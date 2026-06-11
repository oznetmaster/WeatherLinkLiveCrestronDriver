// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License with Commons Clause. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Crestron.DeviceDrivers.EntityModel;
using Crestron.DeviceDrivers.EntityModel.Data;
using Crestron.DeviceDrivers.EntityModel.Logging;
using Crestron.DeviceDrivers.SDK;
using Crestron.DeviceDrivers.SDK.EntityModel;
using Crestron.DeviceDrivers.SDK.EntityModel.Attributes;
using Crestron.DeviceDrivers.SDK.EntityModel.Data;
using Crestron.SimplSharp;

using SimpleWeather;

using WeatherLinkLiveClient = WeatherLinkLive.WeatherLinkLiveAPI.WeatherLinkLive;

namespace WeatherlinkLive.CrestronDriver;

/// <summary>
/// Root Crestron Home Entity V2 weather-station driver that prefers a local WeatherLink Live device
/// for current conditions and falls back to SimpleWeatherClient when the local device is unavailable.
/// </summary>
public sealed class WeatherStationDriver : ReflectedAttributeDriverEntity
	{
	private const int MinimumRefreshIntervalSeconds = 60;
	private const int DefaultRefreshIntervalSeconds = 300;
	private const int WeatherLinkRefreshIntervalSeconds = 30;
	private const int WeatherLinkForceRefreshIntervalSeconds = 120;
	private const double WindySustainedThresholdMph = 15d;
	private const double WindyGustThresholdMph = 20d;
	private const double WindySustainedThresholdKph = 24d;
	private const double WindyGustThresholdKph = 32d;
	private static readonly TimeSpan MinimumOpenWeatherRefreshInterval = TimeSpan.FromMinutes (10);
	private static readonly TimeSpan RequestInactivityWindow = TimeSpan.FromMinutes (1);

	private readonly DriverControllerLogger _logger;
	private readonly string _logControllerId;
	private readonly UiDefinitionProperty _uiDefinition;
	private readonly object _syncLock = new ();
	private readonly object _stateLock = new ();
	private readonly object _cloudWeatherLock = new ();

	private CancellationTokenSource _refreshCancellationTokenSource;
	private int _refreshInProgress;
	private bool _queuedOnDemandRefresh;
	private bool? _lastLocalCurrentAvailable;
	private WeatherSnapshot _lastLocalWeatherSnapshot;
	private string _weatherLinkLiveHost = string.Empty;
	private string _openWeatherApiKey = string.Empty;
	private string _locationNameOverride = string.Empty;
	private double? _latitudeOverride;
	private double? _longitudeOverride;
	private string _units = "metric";
	private int _refreshIntervalSeconds = DefaultRefreshIntervalSeconds;
	private string _pendingWeatherLinkLiveHost = string.Empty;
	private string _pendingOpenWeatherApiKey = string.Empty;
	private string _pendingLocationNameOverride = string.Empty;
	private double? _pendingLatitudeOverride;
	private double? _pendingLongitudeOverride;
	private string _pendingUnits = "metric";
	private int _pendingRefreshIntervalSeconds = DefaultRefreshIntervalSeconds;
	private string _lastSourceSummary = "Source unavailable";
	private string _lastStatus = "Waiting for configuration";
	private string _lastRefreshPhase = "startup";

	private CloudWeatherSnapshot _cloudWeatherSnapshot;
	private DateTime _lastCurrentWeatherRequestUtc;
	private DateTime _lastForecastRequestUtc;
	private DateTime _lastAutomaticCloudRefreshLocalDate;
	private bool _forceCloudRefresh;
	private bool _currentWeatherRequestPending;
	private bool _forecastRequestPending;

	/// <summary>
	/// Initializes a new instance of the <see cref="WeatherStationDriver"/> class.
	/// </summary>
	/// <param name="creationArgs">The Crestron runtime creation arguments.</param>
	/// <param name="resources">The driver implementation resources resolved by the SDK.</param>
	public WeatherStationDriver (DriverControllerCreationArgs creationArgs, DriverImplementationResources resources)
		: base (DriverController.RootControllerId)
		{
		_logger = creationArgs.Logger;
		_logControllerId = creationArgs.DriverId;

		var configurationArgs = DataDrivenConfigurationControllerArgs.FromResources (creationArgs, resources, ControllerId);
		ConfigurationController = new DelegateDataDrivenConfigurationController (configurationArgs, ApplyConfigurationItems, null, null);

		_uiDefinition = UiDefinitionProperty.LoadFromDirectoryIfExists (creationArgs.DriverDataDirectoryPath, resources.InitLogger, LogEntryLevel.Error);
		if (_uiDefinition != null)
			{
			AddProperty (this, UiDefinitionProperty.Name, _uiDefinition);
			}

		try
			{
			AddCommand (this, ExtensionDoCommandExecutor.CommandName, new ExtensionDoCommandExecutor (GetCommand, resources.Logger));
			AddCommand (this, ExtensionSetPropertyValueExecutor.CommandName, new ExtensionSetPropertyValueExecutor (GetCommand, resources.Logger));
			}
		catch (Exception ex)
			{
			LogWarning ("Failed to register extension UI command helpers: " + ex.Message);
			}

		DeviceLabel = "Weather";
		CurrentConditionsTitle = BuildCurrentConditionsTitle (null);
		WeeklyForecastTitle = BuildWeeklyForecastTitle (null);
		CurrentTemperatureDisplay = "--";
		TileDisplay = "--";
		ForecastSummary = "No forecast available";
		TileStatus = "Unavailable";
		WeatherIcon = "icClimateCloudy";
		SourceSummary = _lastSourceSummary;
		LocationSummary = BuildLocationSummary ();
		HumiditySummary = "Humidity --";
		PressureSummary = "Pressure --";
		WindSummary = "Speed --";
		WindDirectionSummary = "Direction --";
		WindGustSummary = "Gust --";
		RainRateSummary = "Rate --";
		RainLast24HoursSummary = "Last 24h --";
		RainChanceSummary = "Chance today --";
		SourceDetailSummary = string.Empty;
		ForecastDay1 = string.Empty;
		ForecastDay2 = string.Empty;
		ForecastDay3 = string.Empty;
		ForecastDay4 = string.Empty;
		ForecastDay5 = string.Empty;
		ForecastDay6 = string.Empty;
		ForecastDay7 = string.Empty;
		ForecastDay1Title = "Today";
		ForecastDay2Title = string.Empty;
		ForecastDay3Title = string.Empty;
		ForecastDay4Title = string.Empty;
		ForecastDay5Title = string.Empty;
		ForecastDay6Title = string.Empty;
		ForecastDay7Title = string.Empty;
		ForecastAttributionLine1 = "Weather data provided by OpenWeather";
		ForecastAttributionLine2 = "https://openweathermap.org/";
		ForecastUpdatedSummary = "Forecast updated --";
		TryPublishUiDefinition ();
		}

	[EntityProperty (Id = "tileDisplay")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string TileDisplay
		{
		get
			{
			return field;
			}
		private set => SetAndNotify ("tileDisplay", value, ref field);
		}

	internal IDriverConfigurationController ConfigurationController
		{
		get;
		}

	[EntityProperty (Id = "onlineIndicator:isOnline")]
	public bool OnlineIndicatorIsOnline
		{
		get;
		private set => SetAndNotify ("onlineIndicator:isOnline", value, ref field);
		}

	[EntityProperty (Id = "readyIndicator:isReady")]
	public bool ReadyIndicatorIsReady
		{
		get;
		private set => SetAndNotify ("readyIndicator:isReady", value, ref field);
		}

	[EntityProperty (Id = "deviceLabel")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string DeviceLabel
		{
		get;
		private set => SetAndNotify ("deviceLabel", value, ref field);
		}

	[EntityProperty (Id = "currentConditionsTitle")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string CurrentConditionsTitle
		{
		get;
		private set => SetAndNotify ("currentConditionsTitle", value, ref field);
		}

	[EntityProperty (Id = "weeklyForecastTitle")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string WeeklyForecastTitle
		{
		get;
		private set => SetAndNotify ("weeklyForecastTitle", value, ref field);
		}

	[EntityProperty (Id = "currentTemperatureDisplay")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string CurrentTemperatureDisplay
		{
		get
			{
			return field;
			}
		private set => SetAndNotify ("currentTemperatureDisplay", value, ref field);
		}

	[EntityProperty (Id = "forecastUpdatedSummary")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string ForecastUpdatedSummary
		{
		get
			{
			return field;
			}
		private set => SetAndNotify ("forecastUpdatedSummary", value, ref field);
		}

	[EntityProperty (Id = "forecastSummary")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string ForecastSummary
		{
		get
			{
			return field;
			}
		private set => SetAndNotify ("forecastSummary", value, ref field);
		}

	[EntityProperty (Id = "tileStatus")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string TileStatus
		{
		get
			{
			return field;
			}
		private set => SetAndNotify ("tileStatus", value, ref field);
		}

	[EntityProperty (Id = "weatherIcon")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string WeatherIcon
		{
		get
			{
			return field;
			}
		private set => SetAndNotify ("weatherIcon", value, ref field);
		}

	[EntityProperty (Id = "sourceSummary")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string SourceSummary
		{
		get
			{
			return field;
			}
		private set => SetAndNotify ("sourceSummary", value, ref field);
		}

	[EntityProperty (Id = "locationSummary")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string LocationSummary
		{
		get;
		private set => SetAndNotify ("locationSummary", value, ref field);
		}

	[EntityProperty (Id = "humiditySummary")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string HumiditySummary
		{
		get
			{
			return field;
			}
		private set => SetAndNotify ("humiditySummary", value, ref field);
		}

	[EntityProperty (Id = "pressureSummary")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string PressureSummary
		{
		get
			{
			return field;
			}
		private set => SetAndNotify ("pressureSummary", value, ref field);
		}

	[EntityProperty (Id = "windSummary")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string WindSummary
		{
		get
			{
			return field;
			}
		private set => SetAndNotify ("windSummary", value, ref field);
		}

	[EntityProperty (Id = "windDirectionSummary")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string WindDirectionSummary
		{
		get
			{
			return field;
			}
		private set => SetAndNotify ("windDirectionSummary", value, ref field);
		}

	[EntityProperty (Id = "windGustSummary")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string WindGustSummary
		{
		get
			{
			return field;
			}
		private set => SetAndNotify ("windGustSummary", value, ref field);
		}

	[EntityProperty (Id = "rainRateSummary")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string RainRateSummary
		{
		get
			{
			return field;
			}
		private set => SetAndNotify ("rainRateSummary", value, ref field);
		}

	[EntityProperty (Id = "rainLast24HoursSummary")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string RainLast24HoursSummary
		{
		get
			{
			return field;
			}
		private set => SetAndNotify ("rainLast24HoursSummary", value, ref field);
		}

	[EntityProperty (Id = "rainChanceSummary")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string RainChanceSummary
		{
		get;
		private set => SetAndNotify ("rainChanceSummary", value, ref field);
		}

	[EntityProperty (Id = "sourceDetailSummary")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string SourceDetailSummary
		{
		get
			{
			return field;
			}
		private set => SetAndNotify ("sourceDetailSummary", value, ref field);
		}

	[EntityProperty (Id = "forecastDay1")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string ForecastDay1
		{
		get
			{
			return field;
			}
		private set => SetAndNotify ("forecastDay1", value, ref field);
		}

	[EntityProperty (Id = "forecastDay2")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string ForecastDay2
		{
		get
			{
			return field;
			}
		private set => SetAndNotify ("forecastDay2", value, ref field);
		}

	[EntityProperty (Id = "forecastDay3")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string ForecastDay3
		{
		get
			{
			return field;
			}
		private set => SetAndNotify ("forecastDay3", value, ref field);
		}

	[EntityProperty (Id = "forecastDay4")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string ForecastDay4
		{
		get
			{
			return field;
			}
		private set => SetAndNotify ("forecastDay4", value, ref field);
		}

	[EntityProperty (Id = "forecastDay5")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string ForecastDay5
		{
		get
			{
			return field;
			}
		private set => SetAndNotify ("forecastDay5", value, ref field);
		}

	[EntityProperty (Id = "forecastDay6")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string ForecastDay6
		{
		get
			{
			return field;
			}
		private set => SetAndNotify ("forecastDay6", value, ref field);
		}

	[EntityProperty (Id = "forecastDay7")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string ForecastDay7
		{
		get
			{
			return field;
			}
		private set => SetAndNotify ("forecastDay7", value, ref field);
		}

	[EntityProperty (Id = "forecastDay1Title")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string ForecastDay1Title
		{
		get
			{
			return field;
			}
		private set => SetAndNotify ("forecastDay1Title", value, ref field);
		}

	[EntityProperty (Id = "forecastDay2Title")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string ForecastDay2Title
		{
		get
			{
			return field;
			}
		private set => SetAndNotify ("forecastDay2Title", value, ref field);
		}

	[EntityProperty (Id = "forecastDay3Title")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string ForecastDay3Title
		{
		get
			{
			return field;
			}
		private set => SetAndNotify ("forecastDay3Title", value, ref field);
		}

	[EntityProperty (Id = "forecastDay4Title")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string ForecastDay4Title
		{
		get
			{
			return field;
			}
		private set => SetAndNotify ("forecastDay4Title", value, ref field);
		}

	[EntityProperty (Id = "forecastDay5Title")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string ForecastDay5Title
		{
		get
			{
			return field;
			}
		private set => SetAndNotify ("forecastDay5Title", value, ref field);
		}

	[EntityProperty (Id = "forecastDay6Title")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string ForecastDay6Title
		{
		get
			{
			return field;
			}
		private set => SetAndNotify ("forecastDay6Title", value, ref field);
		}

	[EntityProperty (Id = "forecastDay7Title")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string ForecastDay7Title
		{
		get
			{
			return field;
			}
		private set => SetAndNotify ("forecastDay7Title", value, ref field);
		}

	[EntityProperty (Id = "forecastAttributionLine1")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string ForecastAttributionLine1
		{
		get
			{
			return field;
			}
		private set => SetAndNotify ("forecastAttributionLine1", value, ref field);
		}

	[EntityProperty (Id = "forecastAttributionLine2")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string ForecastAttributionLine2
		{
		get
			{
			return field;
			}
		private set => SetAndNotify ("forecastAttributionLine2", value, ref field);
		}

	public override void Dispose ()
		{
		StopRefreshLoop ();
		base.Dispose ();
		}

	private ConfigurationItemErrors ApplyConfigurationItems (
		DataDrivenConfigurationController.ApplyConfigurationAction action,
		string stepId,
		IDictionary<string, DriverEntityValue?> values)
		{
		if (action == DataDrivenConfigurationController.ApplyConfigurationAction.ClearValues)
			{
			StopRefreshLoop ();
			_weatherLinkLiveHost = string.Empty;
			_openWeatherApiKey = string.Empty;
			_locationNameOverride = string.Empty;
			_latitudeOverride = null;
			_longitudeOverride = null;
			_units = "metric";
			_refreshIntervalSeconds = DefaultRefreshIntervalSeconds;
			_pendingWeatherLinkLiveHost = string.Empty;
			_pendingOpenWeatherApiKey = string.Empty;
			_pendingLocationNameOverride = string.Empty;
			_pendingLatitudeOverride = null;
			_pendingLongitudeOverride = null;
			_pendingUnits = "metric";
			_pendingRefreshIntervalSeconds = DefaultRefreshIntervalSeconds;
			_lastLocalWeatherSnapshot = null;
			lock (_cloudWeatherLock)
				{
				_cloudWeatherSnapshot = null;
				_lastAutomaticCloudRefreshLocalDate = DateTime.MinValue;
				_forceCloudRefresh = false;
				_currentWeatherRequestPending = false;
				_forecastRequestPending = false;
				_lastCurrentWeatherRequestUtc = DateTime.MinValue;
				_lastForecastRequestUtc = DateTime.MinValue;
				}
			SetUnavailableState ("Configuration cleared");
			return null;
			}

		string openWeatherApiKey = GetString (values, "OpenWeatherApiKey") ?? _pendingOpenWeatherApiKey;
		string weatherLinkLiveHost = GetString (values, "WeatherLinkLiveHost") ?? _pendingWeatherLinkLiveHost;
		string locationNameOverride = GetString (values, "LocationNameOverride") ?? _pendingLocationNameOverride;
		double? latitudeOverride = GetDouble (values, "LatitudeOverride") ?? _pendingLatitudeOverride;
		double? longitudeOverride = GetDouble (values, "LongitudeOverride") ?? _pendingLongitudeOverride;
		string units = NormalizeUnits (GetString (values, "Units") ?? _pendingUnits);
		int refreshInterval = GetInteger (values, "RefreshIntervalSeconds") ?? _pendingRefreshIntervalSeconds;

		_pendingWeatherLinkLiveHost = weatherLinkLiveHost ?? string.Empty;
		_pendingOpenWeatherApiKey = openWeatherApiKey ?? string.Empty;
		_pendingLocationNameOverride = locationNameOverride ?? string.Empty;
		_pendingLatitudeOverride = latitudeOverride;
		_pendingLongitudeOverride = longitudeOverride;
		_pendingUnits = units ?? _pendingUnits;
		_pendingRefreshIntervalSeconds = refreshInterval;

		var errors = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase);
		string configurationError = null;
		if (string.IsNullOrWhiteSpace (openWeatherApiKey))
			{
			errors["OpenWeatherApiKey"] = "OpenWeather API Key is required.";
			}

		if (latitudeOverride.HasValue != longitudeOverride.HasValue)
			{
			errors["LatitudeOverride"] = "Latitude and longitude overrides must both be provided together.";
			errors["LongitudeOverride"] = "Latitude and longitude overrides must both be provided together.";
			}

		if (latitudeOverride.HasValue && (latitudeOverride.Value < -90d || latitudeOverride.Value > 90d))
			{
			errors["LatitudeOverride"] = "Latitude override must be between -90 and 90.";
			}

		if (longitudeOverride.HasValue && (longitudeOverride.Value < -180d || longitudeOverride.Value > 180d))
			{
			errors["LongitudeOverride"] = "Longitude override must be between -180 and 180.";
			}

		if (units == null)
			{
			errors["Units"] = "Units must be Metric, UK, or Imperial.";
			}

		if (refreshInterval < MinimumRefreshIntervalSeconds)
			{
			errors["RefreshIntervalSeconds"] = "Refresh interval must be at least 60 seconds.";
			}

		if (!latitudeOverride.HasValue && !longitudeOverride.HasValue && !HasValidSystemLocation ())
			{
			configurationError = "Crestron Home system location is unavailable. Ensure the processor location is configured.";
			}

		if (errors.Count > 0 || !string.IsNullOrWhiteSpace (configurationError))
			{
			return new ConfigurationItemErrors (
				errors,
				!string.IsNullOrWhiteSpace (configurationError)
					? configurationError
					: "Correct the configuration values and retry.");
			}

		_weatherLinkLiveHost = weatherLinkLiveHost ?? string.Empty;
		_openWeatherApiKey = openWeatherApiKey;
		_locationNameOverride = locationNameOverride ?? string.Empty;
		_latitudeOverride = latitudeOverride;
		_longitudeOverride = longitudeOverride;
		_units = units;
		_refreshIntervalSeconds = refreshInterval;
		_lastLocalWeatherSnapshot = null;
		lock (_cloudWeatherLock)
			{
			_cloudWeatherSnapshot = null;
			_lastAutomaticCloudRefreshLocalDate = DateTime.MinValue;
			_forceCloudRefresh = false;
			_currentWeatherRequestPending = false;
			_forecastRequestPending = false;
			_lastCurrentWeatherRequestUtc = DateTime.MinValue;
			_lastForecastRequestUtc = DateTime.MinValue;
			}
		CurrentConditionsTitle = BuildCurrentConditionsTitle (null);
		WeeklyForecastTitle = BuildWeeklyForecastTitle (null);
		LocationSummary = BuildLocationSummary ();
		StartRefreshLoop ();
		return null;
		}

	private void StartRefreshLoop ()
		{
		StopRefreshLoop ();
		QueueStartupCloudRefresh ();
		lock (_syncLock)
			{
			_refreshCancellationTokenSource = new CancellationTokenSource ();
			_ = Task.Run (() => RefreshLoopAsync (_refreshCancellationTokenSource.Token));
			}
		}

	private void StopRefreshLoop ()
		{
		lock (_syncLock)
			{
			try
				{
				_refreshCancellationTokenSource?.Cancel ();
				}
			catch
				{
				}

			_refreshCancellationTokenSource?.Dispose ();
			_refreshCancellationTokenSource = null;
			}
		}

	private async Task RefreshLoopAsync (CancellationToken cancellationToken)
		{
		try
			{
			_lastRefreshPhase = "initial";
			DebugLog ("RefreshLoop: starting initial refresh.");
			await RunRefreshAsync (cancellationToken).ConfigureAwait (false);
			}
		catch (OperationCanceledException)
			{
			return;
			}
		catch (Exception ex)
			{
			_lastStatus = ex.Message;
			SetUnavailableState ("Weather refresh failed");
			LogError ("Weather refresh failed: " + ex);
			}

		while (!cancellationToken.IsCancellationRequested)
			{
			try
				{
				await Task.Delay (TimeSpan.FromSeconds (_refreshIntervalSeconds), cancellationToken).ConfigureAwait (false);
				}
			catch (OperationCanceledException)
				{
				break;
				}

			try
				{
				_lastRefreshPhase = "scheduled";
				DebugLog ("RefreshLoop: starting scheduled refresh.");
				await RunRefreshAsync (cancellationToken).ConfigureAwait (false);
				}
			catch (OperationCanceledException)
				{
				break;
				}
			catch (Exception ex)
				{
				_lastStatus = ex.Message;
				if (!ReadyIndicatorIsReady)
					{
					SetUnavailableState ("Weather refresh failed");
					}
				LogError ("Weather refresh failed: " + ex);
				}
			}
		}

	private async Task RunRefreshAsync (CancellationToken cancellationToken)
		{
		if (System.Threading.Interlocked.CompareExchange (ref _refreshInProgress, 1, 0) != 0)
			{
			DebugLog ("RunRefreshAsync: refresh already in progress; skipping new request.");
			lock (_syncLock)
				{
				_queuedOnDemandRefresh = true;
				}
			return;
			}

		try
			{
			DebugLog ("RunRefreshAsync: entered refresh execution.");
			await RefreshWeatherAsync (cancellationToken).ConfigureAwait (false);
			}
		finally
			{
			DebugLog ("RunRefreshAsync: leaving refresh execution.");
			System.Threading.Interlocked.Exchange (ref _refreshInProgress, 0);
			lock (_syncLock)
				{
				_queuedOnDemandRefresh = false;
				}
			}
		}

	private void TryStartOnDemandRefresh ()
		{
		CancellationToken cancellationToken;
		bool shouldRefresh;
		DateTime utcNow = DateTime.UtcNow;

		lock (_syncLock)
			{
			cancellationToken = _refreshCancellationTokenSource?.Token ?? CancellationToken.None;
			}

		lock (_cloudWeatherLock)
			{
			bool currentWeatherNeedsCloud = _currentWeatherRequestPending && (string.IsNullOrWhiteSpace (_weatherLinkLiveHost) || _lastLocalCurrentAvailable == false);
			shouldRefresh = (_forecastRequestPending || currentWeatherNeedsCloud) && (_cloudWeatherSnapshot == null || utcNow - _cloudWeatherSnapshot.FetchedAtUtc >= MinimumOpenWeatherRefreshInterval);
			}

		if (!shouldRefresh || cancellationToken.IsCancellationRequested)
			{
			return;
			}

		_ = Task.Run (() => RunRefreshAsync (cancellationToken));
		}

	private async Task RefreshLocalWeatherAsync (CancellationToken cancellationToken)
		{
		DebugLog ("RefreshLocalWeatherAsync: starting local current refresh.");
		WeatherSnapshot localCurrent = await TryGetLocalCurrentWeatherAsync (cancellationToken).ConfigureAwait (false);
		if (localCurrent == null)
			{
			LogInformation (string.IsNullOrWhiteSpace (_weatherLinkLiveHost)
				? "No WeatherLink Live host is configured; using cloud weather fallback."
				: "WeatherLink Live is unavailable; using cloud weather fallback for this update.");
		(double latitude, double longitude) = GetEffectiveCoordinates ();
			CloudWeatherSnapshot cloudWeather = await GetRequestedCloudWeatherSnapshotAsync (latitude, longitude, localCurrentRequired: true, cancellationToken).ConfigureAwait (false);
			WeatherSnapshot fallbackCurrent = BuildFallbackWeatherSnapshot (cloudWeather);
			if (fallbackCurrent != null)
				{
				DebugLog ("RefreshLocalWeatherAsync: applying cloud fallback current snapshot.");
				ApplyWeatherState (fallbackCurrent, refreshStatus: BuildUpdatedSummary ());
				}
			else
				{
				localCurrent = GetCachedLocalWeatherSnapshot ();
				if (localCurrent != null)
					{
					DebugLog ("RefreshLocalWeatherAsync: cloud fallback unavailable; applying cached local snapshot.");
					ApplyWeatherState (MergeForecastIntoSnapshot (localCurrent, GetCachedCloudWeatherSnapshot ()), refreshStatus: BuildRefreshFailedSummary ());
					}
				else
					{
					LogWarning ("RefreshLocalWeatherAsync: no local or cloud weather snapshot available.");
					SetUnavailableState ("Weather unavailable");
					}
				DebugLog ("RefreshLocalWeatherAsync: fallback branch completed.");
				}
			return;
			}

		DebugLog ("RefreshLocalWeatherAsync: applying local current snapshot.");
		ApplyWeatherState (MergeForecastIntoSnapshot (localCurrent, GetCachedCloudWeatherSnapshot ()), refreshStatus: BuildUpdatedSummary ());
		}

	private CloudWeatherSnapshot GetCachedCloudWeatherSnapshot ()
		{
		lock (_cloudWeatherLock)
			{
			return _cloudWeatherSnapshot;
			}
		}

	private WeatherSnapshot GetCachedLocalWeatherSnapshot ()
		{
		lock (_stateLock)
			{
			return _lastLocalWeatherSnapshot;
			}
		}

	private async Task RefreshWeatherAsync (CancellationToken cancellationToken)
		{
		QueueDailyCloudRefreshIfDue ();

		bool forecastRequested;
		lock (_cloudWeatherLock)
			{
			forecastRequested = _forecastRequestPending;
			}
		DebugLog ("RefreshWeatherAsync: forecastRequested=" + forecastRequested + ", hostConfigured=" + !string.IsNullOrWhiteSpace (_weatherLinkLiveHost) + '.');

		(double latitude, double longitude) = GetEffectiveCoordinates ();

		WeatherSnapshot localCurrent = await TryGetLocalCurrentWeatherAsync (cancellationToken).ConfigureAwait (false);
		if (localCurrent != null)
			{
			CloudWeatherSnapshot cachedOrRequestedCloudWeather = forecastRequested
				? await GetRequestedCloudWeatherSnapshotAsync (latitude, longitude, localCurrentRequired: false, cancellationToken).ConfigureAwait (false)
				: GetCachedCloudWeatherSnapshot ();

			DebugLog ("RefreshWeatherAsync: applying local current snapshot after successful WeatherLink Live refresh.");
			localCurrent = MergeForecastIntoSnapshot (localCurrent, cachedOrRequestedCloudWeather);
			ApplyWeatherState (localCurrent, refreshStatus: BuildUpdatedSummary ());
			return;
			}

		LogInformation (string.IsNullOrWhiteSpace (_weatherLinkLiveHost)
			? "No WeatherLink Live host is configured; using cloud weather fallback."
			: "WeatherLink Live is unavailable; using cloud weather fallback for this update.");

		CloudWeatherSnapshot cloudWeather = await GetRequestedCloudWeatherSnapshotAsync (latitude, longitude, localCurrentRequired: true, cancellationToken).ConfigureAwait (false);
		WeatherSnapshot fallbackCurrent = BuildFallbackWeatherSnapshot (cloudWeather);
		if (fallbackCurrent != null)
			{
			DebugLog ("RefreshWeatherAsync: applying cloud fallback after WeatherLink Live failure.");
			ApplyWeatherState (fallbackCurrent, refreshStatus: BuildUpdatedSummary ());
			return;
			}

		localCurrent = GetCachedLocalWeatherSnapshot ();
		if (localCurrent != null)
			{
			DebugLog ("RefreshWeatherAsync: cloud fallback unavailable; applying cached local snapshot.");
			ApplyWeatherState (MergeForecastIntoSnapshot (localCurrent, cloudWeather), refreshStatus: BuildRefreshFailedSummary ());
			return;
			}

		LogWarning ("RefreshWeatherAsync: no current weather snapshot available from WeatherLink Live, cloud, or cache.");
		SetUnavailableState ("Weather unavailable");
		}

	private void QueueStartupCloudRefresh ()
		{
		lock (_cloudWeatherLock)
			{
			_forecastRequestPending = true;
			_lastAutomaticCloudRefreshLocalDate = DateTime.Now.Date;
			}
		}

	private void QueueDailyCloudRefreshIfDue ()
		{
		DateTime now = DateTime.Now;
		if (now.TimeOfDay < TimeSpan.FromMinutes (1))
			{
			return;
			}

		lock (_cloudWeatherLock)
			{
			if (_lastAutomaticCloudRefreshLocalDate >= now.Date)
				{
				return;
				}

			_forecastRequestPending = true;
			_lastAutomaticCloudRefreshLocalDate = now.Date;
			}
		}

	private WeatherSnapshot BuildFallbackWeatherSnapshot (CloudWeatherSnapshot cloudWeather)
		{
		return BuildFallbackWeatherSnapshot (cloudWeather?.Forecast, cloudWeather?.FetchedAtUtc, cloudWeather?.CurrentWeather, cloudWeather?.LocationName);
		}

	private WeatherSnapshot BuildFallbackWeatherSnapshot (WeatherForecast forecast, DateTime? forecastUpdatedUtc, CurrentWeather fallbackCurrent, string locationName)
		{
		if (fallbackCurrent == null)
			{
			return null;
			}

		string description = GetPrimaryWeatherDescription (fallbackCurrent?.Weather);
		string icon = fallbackCurrent?.Weather?.Icon;

		return new WeatherSnapshot
			{
			Temperature = fallbackCurrent?.Main?.Temperature,
			Humidity = fallbackCurrent?.Main?.Humidity,
			WindSpeed = fallbackCurrent?.Wind?.Speed,
			WindGust = null,
			WindDirection = fallbackCurrent?.Wind?.Degree.HasValue == true ? fallbackCurrent.Wind.Degree.Value.ToString ("0", CultureInfo.InvariantCulture) + "°" : string.Empty,
			Pressure = fallbackCurrent?.Main?.Pressure,
			PressureTrend = null,
			RainRate = fallbackCurrent?.Rain?.OneHour,
			RainLast24Hours = null,
			Description = description,
			IconCode = icon,
			LocationName = locationName,
			Forecast = forecast,
			ForecastUpdatedUtc = forecastUpdatedUtc,
			SourceSummary = string.IsNullOrWhiteSpace (_weatherLinkLiveHost)
				? "Source: Online weather service"
				: "Source: Online weather service (WeatherLink Live unavailable)"
			};
		}

	private async Task<CloudWeatherSnapshot> GetRequestedCloudWeatherSnapshotAsync (double latitude, double longitude, bool localCurrentRequired, CancellationToken cancellationToken)
		{
		CloudWeatherSnapshot snapshot;
		DateTime lastCurrentWeatherRequestUtc;
		bool forecastRequestPending;
		bool currentWeatherRequestPending;
		bool forceCloudRefresh;
		lock (_cloudWeatherLock)
			{
			snapshot = _cloudWeatherSnapshot;
			lastCurrentWeatherRequestUtc = _lastCurrentWeatherRequestUtc;
			forecastRequestPending = _forecastRequestPending;
			currentWeatherRequestPending = _currentWeatherRequestPending;
			forceCloudRefresh = _forceCloudRefresh;
			}

		DateTime utcNow = DateTime.UtcNow;
		bool forecastRequested = forecastRequestPending;
		bool currentWeatherRequested = localCurrentRequired && (currentWeatherRequestPending || snapshot == null || lastCurrentWeatherRequestUtc == DateTime.MinValue);
		DebugLog ("GetRequestedCloudWeatherSnapshotAsync: forecastRequested=" + forecastRequested + ", currentWeatherRequested=" + currentWeatherRequested + ", hasSnapshot=" + (snapshot != null) + ", forceRefresh=" + forceCloudRefresh + '.');

		if (!forecastRequested && !currentWeatherRequested)
			{
			DebugLog ("GetRequestedCloudWeatherSnapshotAsync: returning cached snapshot without refresh.");
			return snapshot;
			}

		if (!forceCloudRefresh && snapshot != null && utcNow - snapshot.FetchedAtUtc < MinimumOpenWeatherRefreshInterval)
			{
			DebugLog ("GetRequestedCloudWeatherSnapshotAsync: throttled; using cached snapshot fetched at " + snapshot.FetchedAtUtc.ToString ("o", CultureInfo.InvariantCulture) + '.');
			return snapshot;
			}

		DebugLog ("GetRequestedCloudWeatherSnapshotAsync: requesting cloud forecast/current snapshot.");
		using var weatherController = new WeatherController (_openWeatherApiKey);
		var geoLocator = new GeoLocator (_openWeatherApiKey);
		var coordinates = new LatLong
			{
			Latitude = latitude,
			Longitude = longitude
			};
		WeatherForecast forecast = await weatherController.GetWeatherForecastAsync (coordinates, OpenWeatherUnits, cancellationToken).ConfigureAwait (false);
		CurrentWeather currentWeather = await weatherController.GetCurrentWeatherAsync (coordinates, OpenWeatherUnits, cancellationToken).ConfigureAwait (false);
		string locationName = currentWeather?.City;
		if (string.IsNullOrWhiteSpace (locationName))
			{
			try
				{
				locationName = await geoLocator.GetCityNameByCoordinatesAsync (coordinates, cancellationToken).ConfigureAwait (false);
				}
			catch (Exception ex)
				{
				DebugLog ("GetRequestedCloudWeatherSnapshotAsync: location lookup failed - " + ex.Message);
				}
			}
		var refreshedSnapshot = new CloudWeatherSnapshot (forecast, currentWeather, utcNow, locationName);

		lock (_cloudWeatherLock)
			{
			_cloudWeatherSnapshot = refreshedSnapshot;
			if (forecastRequested)
				{
				_forecastRequestPending = false;
				}

			_forceCloudRefresh = false;

			if (currentWeatherRequested)
				{
				_currentWeatherRequestPending = false;
				}
			}
		DebugLog ("GetRequestedCloudWeatherSnapshotAsync: cloud snapshot refreshed successfully.");

		return refreshedSnapshot;
		}

	private void MarkCurrentWeatherRequested ()
		{
		lock (_cloudWeatherLock)
			{
			DateTime utcNow = DateTime.UtcNow;
			if (utcNow - _lastCurrentWeatherRequestUtc > RequestInactivityWindow)
				{
				_currentWeatherRequestPending = true;
				}

			_lastCurrentWeatherRequestUtc = utcNow;
			}
		}

	private void MarkForecastRequested ()
		{
		lock (_cloudWeatherLock)
			{
			DateTime utcNow = DateTime.UtcNow;
			if (utcNow - _lastForecastRequestUtc > RequestInactivityWindow)
				{
				_forecastRequestPending = true;
				}

			_lastForecastRequestUtc = utcNow;
			}
		}

	private async Task<WeatherSnapshot> TryGetLocalCurrentWeatherAsync (CancellationToken cancellationToken)
		{
		if (string.IsNullOrWhiteSpace (_weatherLinkLiveHost))
			{
			_lastLocalCurrentAvailable = false;
			_lastSourceSummary = "Source: Online weather service";
			LogInformation ("WeatherLink Live host is blank; local weather is disabled.");
			return null;
			}

		try
			{
			DebugLog ("Attempting WeatherLink Live refresh from host " + _weatherLinkLiveHost + '.');
			using var client = new WeatherLinkLiveClient (_weatherLinkLiveHost, WeatherLinkRefreshIntervalSeconds, WeatherLinkForceRefreshIntervalSeconds, UseMetricTemperatureUnits, UseMetricRainUnits, UseMetricWindUnits, UseMetricBarometerUnits);
			DebugLog ("TryGetLocalCurrentWeatherAsync: calling InitializeAsync.");
			await client.InitializeAsync (cancellationToken).ConfigureAwait (false);
			DebugLog ("TryGetLocalCurrentWeatherAsync: InitializeAsync completed.");
			_lastLocalCurrentAvailable = true;
			_lastSourceSummary = "Source: WeatherLink Live + online forecast";
			DebugLog ("WeatherLink Live refresh succeeded.");
			var snapshot = new WeatherSnapshot
				{
				Temperature = client.Temperature,
				Humidity = client.Humidity,
				WindSpeed = client.WindSpeedLast,
				WindGust = client.WindSpeedHighLast10Minutes,
				WindDirection = client.WindDirectionLastCompass,
				Pressure = client.BarometerAtSeaLevel,
				PressureTrend = string.IsNullOrWhiteSpace (client.BarometerTrend) ? null : client.BarometerTrend,
				RainRate = client.RainRate,
				RainLast24Hours = client.RainfallLast24Hours,
				Description = string.IsNullOrWhiteSpace (client.BarometerTrend) ? "Live weather data" : client.BarometerTrend,
				IconCode = null,
				SourceSummary = _lastSourceSummary,
				IsLocalCurrent = true
				};
			lock (_stateLock)
				{
				_lastLocalWeatherSnapshot = snapshot;
				}
			return snapshot;
			}
		catch (Exception ex)
			{
			_lastStatus = ex.Message;
			_lastLocalCurrentAvailable = false;
			_lastSourceSummary = "Source: WeatherLink Live unavailable";
			LogWarning ("WeatherLink Live refresh failed for host " + _weatherLinkLiveHost + ": " + ex);
			return null;
			}
		}

	[EntityCommand (Id = "weatherRequestForecast")]
	public void RequestForecastRefresh ()
		{
		_lastRefreshPhase = "forecast-command";
		MarkForecastRequested ();
		TryStartOnDemandRefresh ();
		}

	private WeatherSnapshot MergeForecastIntoSnapshot (WeatherSnapshot currentSnapshot, CloudWeatherSnapshot cloudWeather)
		{
		if (currentSnapshot == null || cloudWeather == null)
			{
			return currentSnapshot;
			}

		return new WeatherSnapshot
			{
			Temperature = currentSnapshot.Temperature,
			Humidity = currentSnapshot.Humidity,
			WindSpeed = currentSnapshot.WindSpeed,
			WindGust = currentSnapshot.WindGust,
			WindDirection = currentSnapshot.WindDirection,
			Pressure = currentSnapshot.Pressure,
			PressureTrend = currentSnapshot.PressureTrend,
			RainRate = currentSnapshot.RainRate,
			RainLast24Hours = currentSnapshot.RainLast24Hours,
			Description = currentSnapshot.Description,
			IconCode = currentSnapshot.IconCode,
			CloudDescription = GetPrimaryWeatherDescription (cloudWeather.CurrentWeather?.Weather),
			CloudIconCode = cloudWeather.CurrentWeather?.Weather?.Icon,
			LocationName = currentSnapshot.LocationName ?? cloudWeather.LocationName,
			Forecast = cloudWeather.Forecast,
			ForecastUpdatedUtc = cloudWeather.FetchedAtUtc,
			SourceSummary = currentSnapshot.SourceSummary,
			IsLocalCurrent = currentSnapshot.IsLocalCurrent
			};
		}

	private void ApplyWeatherState (WeatherSnapshot current, string refreshStatus)
		{
		string weatherIcon = MapWeatherIcon (current);
		DebugLog ("ApplyWeatherState: source=" + (current?.SourceSummary ?? "<null>") + ", temp=" + (current?.Temperature.HasValue == true ? current.Temperature.Value.ToString (CultureInfo.InvariantCulture) : "<null>") + ", humidity=" + (current?.Humidity.HasValue == true ? current.Humidity.Value.ToString (CultureInfo.InvariantCulture) : "<null>") + ", forecastPresent=" + (current?.Forecast != null) + ", refreshStatus=" + (refreshStatus ?? "<null>") + '.');
		DebugLog (string.Format (
			CultureInfo.InvariantCulture,
			"ApplyWeatherState icon={0}, isLocal={1}, rainRate={2}, description={3}, iconCode={4}, cloudDescription={5}, cloudIconCode={6}",
			weatherIcon ?? "<null>",
			current?.IsLocalCurrent,
			current?.RainRate.HasValue == true ? current.RainRate.Value.ToString (CultureInfo.InvariantCulture) : "<null>",
			current?.Description ?? "<null>",
			current?.IconCode ?? "<null>",
			current?.CloudDescription ?? "<null>",
			current?.CloudIconCode ?? "<null>"));
		CurrentTemperatureDisplay = FormatTemperature (current.Temperature);
		TileDisplay = BuildTileDisplay (current);
		TileStatus = refreshStatus ?? BuildUpdatedSummary ();
		WeatherIcon = weatherIcon;
		CurrentConditionsTitle = BuildCurrentConditionsTitle (current.LocationName);
		WeeklyForecastTitle = BuildWeeklyForecastTitle (current.LocationName);
		SourceSummary = current.SourceSummary ?? _lastSourceSummary;
		LocationSummary = BuildLocationSummary ();
		HumiditySummary = BuildHumiditySummary (current);
		PressureSummary = BuildPressureSummary (current, UseMetricBarometerUnits);
		WindSummary = BuildWindSummary (current, UseMetricWindUnits);
		WindDirectionSummary = BuildWindDirectionSummary (current);
		WindGustSummary = BuildWindGustSummary (current, UseMetricWindUnits);
		RainRateSummary = BuildRainRateSummary (current, UseMetricRainUnits);
		RainLast24HoursSummary = BuildRainLast24HoursSummary (current, UseMetricRainUnits);
		SourceDetailSummary = BuildSourceDetailSummary (refreshStatus);
		if (current.Forecast != null)
			{
			ForecastSummary = BuildForecastSummary (current.Forecast);
			RainChanceSummary = BuildRainChanceSummary (current.Forecast);
			ForecastUpdatedSummary = BuildForecastUpdatedSummary (current.ForecastUpdatedUtc ?? DateTime.UtcNow);
			ApplyForecastDays (current.Forecast);
			}
		OnlineIndicatorIsOnline = true;
		ReadyIndicatorIsReady = true;
		TryPublishUiDefinition ();
		}

	private void ApplyForecastDays (WeatherForecast forecast)
		{
		IReadOnlyList<Daily> days = forecast?.Daily != null ? forecast.Daily : (IReadOnlyList<Daily>)Array.Empty<Daily> ();
		ForecastDay1Title = BuildForecastDayTitle (days, 0);
		ForecastDay2Title = BuildForecastDayTitle (days, 1);
		ForecastDay3Title = BuildForecastDayTitle (days, 2);
		ForecastDay4Title = BuildForecastDayTitle (days, 3);
		ForecastDay5Title = BuildForecastDayTitle (days, 4);
		ForecastDay6Title = BuildForecastDayTitle (days, 5);
		ForecastDay7Title = BuildForecastDayTitle (days, 6);
		ForecastDay1 = FormatForecastDay (days, 0);
		ForecastDay2 = FormatForecastDay (days, 1);
		ForecastDay3 = FormatForecastDay (days, 2);
		ForecastDay4 = FormatForecastDay (days, 3);
		ForecastDay5 = FormatForecastDay (days, 4);
		ForecastDay6 = FormatForecastDay (days, 5);
		ForecastDay7 = FormatForecastDay (days, 6);
		}

	private static string BuildHumiditySummary (WeatherSnapshot current)
	=> current.Humidity.HasValue
		? "Humidity " + current.Humidity.Value.ToString ("0", CultureInfo.InvariantCulture) + "%"
		: "Humidity --";

	private static string BuildPressureSummary (WeatherSnapshot current, bool isMetricUnits)
	=> current.Pressure.HasValue
		? "Pressure " + current.Pressure.Value.ToString ("0.0", CultureInfo.InvariantCulture) + " " + GetPressureUnit (isMetricUnits) + FormatPressureTrend (current.PressureTrend)
		: "Pressure --";

	private static string BuildWindSummary (WeatherSnapshot current, bool isMetricUnits)
	=> current.WindSpeed.HasValue
		? "Speed " + current.WindSpeed.Value.ToString ("0.0", CultureInfo.InvariantCulture) + " " + GetWindUnit (isMetricUnits)
		: "Speed --";

	private static string BuildWindDirectionSummary (WeatherSnapshot current)
	=> !string.IsNullOrWhiteSpace (current.WindDirection)
		? "Direction " + current.WindDirection
		: "Direction --";

	private static string BuildWindGustSummary (WeatherSnapshot current, bool isMetricUnits)
	=> current.WindGust.HasValue
		? "Gust " + current.WindGust.Value.ToString ("0.0", CultureInfo.InvariantCulture) + " " + GetWindUnit (isMetricUnits)
		: "Gust --";

	private static string BuildRainRateSummary (WeatherSnapshot current, bool isMetricUnits)
	=> current.RainRate.HasValue
		? "Rate " + current.RainRate.Value.ToString ("0.0", CultureInfo.InvariantCulture) + " " + GetRainRateUnit (isMetricUnits)
		: "Rate --";

	private static string BuildRainLast24HoursSummary (WeatherSnapshot current, bool isMetricUnits)
	=> current.RainLast24Hours.HasValue
		? "Last 24h " + current.RainLast24Hours.Value.ToString ("0.0", CultureInfo.InvariantCulture) + " " + GetRainTotalUnit (isMetricUnits)
		: "Last 24h --";

	private static string BuildRainChanceSummary (WeatherForecast forecast)
		{
		Daily today = forecast?.Daily != null && forecast.Daily.Count > 0 ? forecast.Daily[0] : null;
		return today?.PrecipitationProbability.HasValue == true
			? "Chance today " + today.PrecipitationProbability.Value.ToString ("0", CultureInfo.InvariantCulture) + "%"
			: "Chance today --";
		}

	private string BuildSourceDetailSummary (string refreshStatus)
	=> !string.IsNullOrWhiteSpace (refreshStatus) && refreshStatus.EndsWith (" failed", StringComparison.OrdinalIgnoreCase)
		? _lastStatus
		: string.Empty;

	private static string FormatPressureTrend (string pressureTrend)
	=> string.IsNullOrWhiteSpace (pressureTrend) ? string.Empty : " | " + pressureTrend;

	private static string GetPressureUnit (bool isMetricUnits) => isMetricUnits ? "hPa" : "inHg";

	private static string GetWindUnit (bool isMetricUnits) => isMetricUnits ? "kph" : "mph";

	private static string GetRainRateUnit (bool isMetricUnits) => isMetricUnits ? "mm/hr" : "in/hr";

	private static string GetRainTotalUnit (bool isMetricUnits) => isMetricUnits ? "mm" : "in";

	private static object GetPropertyValue (object instance, string propertyName)
	=> instance?.GetType ().GetProperty (propertyName)?.GetValue (instance, null);

	private static double? GetNullableDoubleProperty (object instance, string propertyName)
		{
		object value = GetPropertyValue (instance, propertyName);
		if (value == null)
			{
			return null;
			}

		if (value is double doubleValue)
			{
			return doubleValue;
			}

		if (value is float floatValue)
			{
			return floatValue;
			}

		if (value is decimal decimalValue)
			{
			return (double)decimalValue;
			}

		if (value is int intValue)
			{
			return intValue;
			}

		if (value is long longValue)
			{
			return longValue;
			}

		if (double.TryParse (value.ToString (), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double parsed))
			{
			return parsed;
			}

		return null;
		}

	private string BuildForecastSummary (WeatherForecast forecast)
		{
		IReadOnlyList<Daily> days = forecast?.Daily != null ? forecast.Daily : (IReadOnlyList<Daily>)Array.Empty<Daily> ();
		if (days.Count == 0)
			{
			return "No forecast data returned.";
			}

		double? highestTemperature = null;
		double? lowestTemperature = null;
		double? highestPrecipitation = null;
		int forecastDays = Math.Min (7, days.Count);

		for (int i = 0; i < forecastDays; i++)
			{
			Daily day = days[i];
			if (day?.Temperature?.Max.HasValue == true)
				{
				highestTemperature = !highestTemperature.HasValue || day.Temperature.Max.Value > highestTemperature.Value
					? day.Temperature.Max.Value
					: highestTemperature;
				}

			if (day?.Temperature?.Min.HasValue == true)
				{
				lowestTemperature = !lowestTemperature.HasValue || day.Temperature.Min.Value < lowestTemperature.Value
					? day.Temperature.Min.Value
					: lowestTemperature;
				}

			if (day?.PrecipitationProbability.HasValue == true)
				{
				highestPrecipitation = !highestPrecipitation.HasValue || day.PrecipitationProbability.Value > highestPrecipitation.Value
					? day.PrecipitationProbability.Value
					: highestPrecipitation;
				}
			}

		return string.Format (
			CultureInfo.InvariantCulture,
			"{0}-day outlook | H {1} | L {2} | Max rain {3}",
			forecastDays,
			FormatTemperature (highestTemperature, UseMetricForecastTemperatureUnits),
			FormatTemperature (lowestTemperature, UseMetricForecastTemperatureUnits),
			highestPrecipitation.HasValue ? highestPrecipitation.Value.ToString ("0", CultureInfo.InvariantCulture) + "%" : "--");
		}

	private string FormatForecastDay (IReadOnlyList<Daily> days, int index)
		{
		if (index >= days.Count || days[index] == null)
			{
			return string.Empty;
			}

		Daily day = days[index];
		string description = GetPrimaryWeatherDescription (day.Weather);
		string high = FormatTemperature (day.Temperature?.Max, UseMetricForecastTemperatureUnits);
		string low = FormatTemperature (day.Temperature?.Min, UseMetricForecastTemperatureUnits);
		string precipitation = day.PrecipitationProbability.HasValue ? day.PrecipitationProbability.Value.ToString ("0", CultureInfo.InvariantCulture) + "%" : "--";
		return string.Format (CultureInfo.InvariantCulture, "{0} | H {1} | L {2} | Rain {3}", description, high, low, precipitation);
		}

	private static string BuildForecastDayTitle (IReadOnlyList<Daily> days, int index)
		{
		if (index >= days.Count || days[index] == null)
			{
			return string.Empty;
			}

		DateTime forecastDate = Convert.ToDateTime (days[index].DT, CultureInfo.InvariantCulture).Date;
		if (index == 0 || forecastDate == DateTime.Today)
			{
			return "Today";
			}

		DateTimeFormatInfo dateFormat = CultureInfo.CurrentCulture.DateTimeFormat;
		return string.Format (
			CultureInfo.CurrentCulture,
			"{0} {1}",
			forecastDate.ToString ("dddd", CultureInfo.CurrentCulture),
			forecastDate.ToString (dateFormat.MonthDayPattern, CultureInfo.CurrentCulture));
		}

	private static string BuildUpdatedSummary ()
	=> string.Format (CultureInfo.CurrentCulture, "Updated {0}", DateTime.Now.ToString ("t", CultureInfo.CurrentCulture));

	private static string BuildRefreshFailedSummary ()
	=> string.Format (CultureInfo.CurrentCulture, "Updated {0} failed", DateTime.Now.ToString ("t", CultureInfo.CurrentCulture));

	private static string BuildForecastUpdatedSummary (DateTime fetchedAtUtc)
	=> string.Format (CultureInfo.CurrentCulture, "Forecast updated {0}", fetchedAtUtc.ToLocalTime ().ToString ("t", CultureInfo.CurrentCulture));

	private void SetUnavailableState (string status)
		{
		CurrentTemperatureDisplay = "--";
		ForecastSummary = "Forecast unavailable.";
		TileStatus = status;
		WeatherIcon = "icClimateRegular";
		CurrentConditionsTitle = BuildCurrentConditionsTitle (null);
		WeeklyForecastTitle = BuildWeeklyForecastTitle (null);
		SourceSummary = _lastSourceSummary;
		LocationSummary = BuildLocationSummary ();
		HumiditySummary = "Humidity --";
		PressureSummary = "Pressure --";
		WindSummary = "Speed --";
		WindDirectionSummary = "Direction --";
		WindGustSummary = "Gust --";
		RainRateSummary = "Rate --";
		RainLast24HoursSummary = "Last 24h --";
		RainChanceSummary = "Chance today --";
		SourceDetailSummary = string.Empty;
		ForecastDay1 = string.Empty;
		ForecastDay2 = string.Empty;
		ForecastDay3 = string.Empty;
		ForecastDay4 = string.Empty;
		ForecastDay5 = string.Empty;
		ForecastDay6 = string.Empty;
		ForecastDay7 = string.Empty;
		ForecastUpdatedSummary = "Forecast updated --";
		ForecastDay1Title = "Today";
		ForecastDay2Title = string.Empty;
		ForecastDay3Title = string.Empty;
		ForecastDay4Title = string.Empty;
		ForecastDay5Title = string.Empty;
		ForecastDay6Title = string.Empty;
		ForecastDay7Title = string.Empty;
		OnlineIndicatorIsOnline = true;
		ReadyIndicatorIsReady = true;
		TryPublishUiDefinition ();
		}

	private static string GetString (IDictionary<string, DriverEntityValue?> values, string key)
		{
		if (values == null || !values.TryGetValue (key, out DriverEntityValue? value) || !value.HasValue)
			{
			return null;
			}
		return value.Value.ToString ();
		}

	private static int? GetInteger (IDictionary<string, DriverEntityValue?> values, string key)
		{
		string raw = GetString (values, key);
		if (string.IsNullOrWhiteSpace (raw))
			{
			return null;
			}
		return int.TryParse (raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : null;
		}

	private static double? GetDouble (IDictionary<string, DriverEntityValue?> values, string key)
		{
		string raw = GetString (values, key);
		if (string.IsNullOrWhiteSpace (raw))
			{
			return null;
			}
		return double.TryParse (raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double parsed) ? parsed : null;
		}

	private static string NormalizeUnits (string units)
		{
		if (string.IsNullOrWhiteSpace (units) || string.Equals (units, "Metric", StringComparison.OrdinalIgnoreCase) || string.Equals (units, "metric", StringComparison.OrdinalIgnoreCase))
			{
			return "metric";
			}
		if (string.Equals (units, "UK", StringComparison.OrdinalIgnoreCase) || string.Equals (units, "uk", StringComparison.OrdinalIgnoreCase))
			{
			return "uk";
			}
		if (string.Equals (units, "Imperial", StringComparison.OrdinalIgnoreCase) || string.Equals (units, "imperial", StringComparison.OrdinalIgnoreCase))
			{
			return "imperial";
			}
		return null;
		}

	private bool UseMetricTemperatureUnits => string.Equals (_units, "metric", StringComparison.OrdinalIgnoreCase) || string.Equals (_units, "uk", StringComparison.OrdinalIgnoreCase);

	private bool UseMetricRainUnits => string.Equals (_units, "metric", StringComparison.OrdinalIgnoreCase) || string.Equals (_units, "uk", StringComparison.OrdinalIgnoreCase);

	private bool UseMetricWindUnits => string.Equals (_units, "metric", StringComparison.OrdinalIgnoreCase);

	private bool UseMetricBarometerUnits => string.Equals (_units, "metric", StringComparison.OrdinalIgnoreCase);

	private string OpenWeatherUnits => string.Equals (_units, "imperial", StringComparison.OrdinalIgnoreCase) ? "imperial" : "metric";

	private bool UseMetricForecastTemperatureUnits => string.Equals (OpenWeatherUnits, "metric", StringComparison.OrdinalIgnoreCase);

	private static bool HasValidSystemLocation () => !double.IsNaN (SystemLocation.Latitude) && !double.IsNaN (SystemLocation.Longitude) && (SystemLocation.Latitude != 0d || SystemLocation.Longitude != 0d);

	private (double Latitude, double Longitude) GetEffectiveCoordinates ()
	=> (_latitudeOverride ?? SystemLocation.Latitude, _longitudeOverride ?? SystemLocation.Longitude);

	private string BuildLocationSummary ()
		{
		(double latitude, double longitude) = GetEffectiveCoordinates ();
		return string.Format (CultureInfo.InvariantCulture, "{0:0.0000}, {1:0.0000}", latitude, longitude);
		}

	private string BuildCurrentConditionsTitle (string locationName)
	=> !string.IsNullOrWhiteSpace (_locationNameOverride)
		? _locationNameOverride
		: !string.IsNullOrWhiteSpace (locationName)
			? locationName
			: BuildLocationSummary ();

	private string BuildWeeklyForecastTitle (string locationName)
	=> !string.IsNullOrWhiteSpace (_locationNameOverride)
		? _locationNameOverride
		: !string.IsNullOrWhiteSpace (locationName)
			? locationName
			: BuildLocationSummary ();

	private string FormatTemperature (double? value)
	=> FormatTemperature (value, UseMetricTemperatureUnits);

	private static string FormatTemperature (double? value, bool useMetricUnits)
	=> value.HasValue ? value.Value.ToString ("0.0", CultureInfo.InvariantCulture) + (useMetricUnits ? "°C" : "°F") : "--";

	private string BuildTileDisplay (WeatherSnapshot current)
		{
		string temperature = FormatRoundedTemperature (current?.Temperature, UseMetricTemperatureUnits);
		string humidity = current?.Humidity.HasValue == true ? current.Humidity.Value.ToString ("0", CultureInfo.InvariantCulture) + "%" : "--";
		return string.Format (CultureInfo.InvariantCulture, "{0} | {1}", temperature, humidity);
		}

	private static string FormatRoundedTemperature (double? value, bool useMetricUnits)
	=> value.HasValue ? Math.Round (value.Value, MidpointRounding.AwayFromZero).ToString ("0", CultureInfo.InvariantCulture) + (useMetricUnits ? "°C" : "°F") : "--";

	private static string GetPrimaryWeatherDescription (object weatherValue)
		{
		if (weatherValue == null)
			{
			return "Unknown conditions";
			}

		if (weatherValue is System.Collections.IEnumerable enumerable && weatherValue is not string)
			{
			foreach (object item in enumerable)
				{
				return GetPrimaryWeatherDescription (item);
				}

			return "Unknown conditions";
			}

		Type type = weatherValue.GetType ();
		var descriptionProperty = type.GetProperty ("Description");
		string description = descriptionProperty?.GetValue (weatherValue, null) as string;
		if (!string.IsNullOrWhiteSpace (description))
			{
			return description;
			}

		var mainProperty = type.GetProperty ("Main");
		string main = mainProperty?.GetValue (weatherValue, null) as string;
		return !string.IsNullOrWhiteSpace (main) ? main : weatherValue.ToString ();
		}

	private string MapWeatherIcon (WeatherSnapshot current)
		{
		if (current == null)
			{
			return "icClimateRegular";
			}

		if (!current.IsLocalCurrent)
			{
			return MapCloudWeatherIcon (current.IconCode, current.Description, suppressRain: false);
			}

		string localIcon = MapLocalWeatherIcon (current, UseMetricTemperatureUnits);
		if (!string.Equals (localIcon, "icClimateRegular", StringComparison.Ordinal))
			{
			return localIcon;
			}

		string cloudIcon = MapCloudWeatherIcon (current.CloudIconCode, current.CloudDescription, suppressRain: current.RainRate.HasValue && current.RainRate.Value <= 0);
		return cloudIcon;
		}

	private static string MapLocalWeatherIcon (WeatherSnapshot current, bool useMetricTemperatureUnits)
		{
		if (current.RainRate.HasValue && current.RainRate.Value > 0)
			{
			return IsBelowFreezing (current.Temperature, useMetricTemperatureUnits)
				? "icCoolingRegular"
				: "icHumidifying";
			}

		if (IsWindy (current, useMetricTemperatureUnits))
			{
			return "icFanOn";
			}

		return "icClimateRegular";
		}

	private static bool IsBelowFreezing (double? temperature, bool useMetricTemperatureUnits)
	=> temperature.HasValue && temperature.Value < (useMetricTemperatureUnits ? 0d : 32d);

	private static bool IsWindy (WeatherSnapshot current, bool useMetricTemperatureUnits)
		{
		double sustainedThreshold = useMetricTemperatureUnits ? WindySustainedThresholdKph : WindySustainedThresholdMph;
		double gustThreshold = useMetricTemperatureUnits ? WindyGustThresholdKph : WindyGustThresholdMph;
		return (current.WindSpeed.HasValue && current.WindSpeed.Value >= sustainedThreshold)
			|| (current.WindGust.HasValue && current.WindGust.Value >= gustThreshold);
		}

	private static string MapCloudWeatherIcon (string iconCode, string description, bool suppressRain)
		{
		if (string.IsNullOrWhiteSpace (iconCode))
			{
			return MapWeatherDescriptionIcon (description, suppressRain);
			}

		string normalized = iconCode.ToLowerInvariant ();
		if (normalized.StartsWith ("01", StringComparison.Ordinal))
			return "icSun";
		if (normalized.StartsWith ("02", StringComparison.Ordinal) || normalized.StartsWith ("03", StringComparison.Ordinal) || normalized.StartsWith ("04", StringComparison.Ordinal) || normalized.StartsWith ("50", StringComparison.Ordinal))
			return "icSmallSun";
		if (!suppressRain && (normalized.StartsWith ("09", StringComparison.Ordinal) || normalized.StartsWith ("10", StringComparison.Ordinal)))
			return "icHumidifying";
		if (normalized.StartsWith ("11", StringComparison.Ordinal))
			return "icQuickAction";
		if (normalized.StartsWith ("13", StringComparison.Ordinal))
			return "icCoolingRegular";
		return MapWeatherDescriptionIcon (description, suppressRain);
		}

	private static string MapWeatherDescriptionIcon (string description, bool suppressRain)
		{
		if (string.IsNullOrWhiteSpace (description))
			{
		return "icSmallSun";
			}

		string normalized = description.ToLowerInvariant ();
		if (normalized.Contains ("thunder") || normalized.Contains ("storm"))
			return "icQuickAction";
		if (normalized.Contains ("snow") || normalized.Contains ("sleet") || normalized.Contains ("ice") || normalized.Contains ("hail"))
			return "icCoolingRegular";
		if (normalized.Contains ("rain") || normalized.Contains ("drizzle") || normalized.Contains ("shower"))
			return suppressRain ? "icSmallSun" : "icHumidifying";
		if (normalized.Contains ("wind") || normalized.Contains ("breez") || normalized.Contains ("gust"))
			return "icFanOn";
		if (normalized.Contains ("fog") || normalized.Contains ("mist") || normalized.Contains ("haze") || normalized.Contains ("smoke") || normalized.Contains ("overcast") || normalized.Contains ("cloud"))
			return "icSmallSun";
		if (normalized.Contains ("clear") || normalized.Contains ("sun"))
			return "icSun";
		return "icClimateRegular";
		}

	private void TryPublishUiDefinition ()
		{
		if (_uiDefinition == null)
			{
			return;
			}

		DriverEntityValue? uiDefinitionValue = _uiDefinition.GetValue (null, null);
		if (uiDefinitionValue.HasValue)
			{
			NotifyPropertyChanged (UiDefinitionProperty.Name, uiDefinitionValue.Value);
			}
		}

	private void SetAndNotify (string propertyId, string value, ref string backingField)
		{
		value ??= string.Empty;
		string previousValue;
		lock (_stateLock)
			{
			previousValue = backingField;
			if (string.Equals (backingField, value, StringComparison.Ordinal))
				{
				DebugLog ("SetAndNotify: phase=" + _lastRefreshPhase + ", property=" + propertyId + ", unchanged value=" + value + '.');
				return;
				}

			backingField = value;
			}

		DebugLog ("SetAndNotify: phase=" + _lastRefreshPhase + ", property=" + propertyId + ", old=" + previousValue + ", new=" + value + '.');
		NotifyPropertyChanged (propertyId, new DriverEntityValue (value));
		}

	private void SetAndNotify (string propertyId, bool value, ref bool backingField)
		{
		bool previousValue;
		lock (_stateLock)
			{
			previousValue = backingField;
			if (backingField == value)
				{
				DebugLog ("SetAndNotify: phase=" + _lastRefreshPhase + ", property=" + propertyId + ", unchanged value=" + value + '.');
				return;
				}

			backingField = value;
			}

		DebugLog ("SetAndNotify: phase=" + _lastRefreshPhase + ", property=" + propertyId + ", old=" + previousValue + ", new=" + value + '.');
		NotifyPropertyChanged (propertyId, new DriverEntityValue (value));
		}

	[Conditional ("DEBUG")]
	private void DebugLog (string message) => LogInformation (message);

	private void LogWarning (string message) => _logger?.Log (_logControllerId, LogEntryLevel.Warning, message);

	private void LogError (string message) => _logger?.Log (_logControllerId, LogEntryLevel.Error, message);

	private void LogInformation (string message) => _logger?.Log (_logControllerId, LogEntryLevel.Info, message);

	private sealed class CloudWeatherSnapshot
		{
		public CloudWeatherSnapshot (WeatherForecast forecast, CurrentWeather currentWeather, DateTime fetchedAtUtc, string locationName)
			{
			Forecast = forecast;
			CurrentWeather = currentWeather;
			FetchedAtUtc = fetchedAtUtc;
			LocationName = locationName;
			}

		public WeatherForecast Forecast
			{
			get;
			}
		public CurrentWeather CurrentWeather
			{
			get;
			}
		public DateTime FetchedAtUtc
			{
			get;
			}
		public string LocationName
			{
			get;
			}
		}

	private sealed class WeatherSnapshot
		{
		public double? Temperature
			{
			get; set;
			}
		public double? Humidity
			{
			get; set;
			}
		public double? WindSpeed
			{
			get; set;
			}
		public double? WindGust
			{
			get; set;
			}
		public string WindDirection
			{
			get; set;
			}
		public double? Pressure
			{
			get; set;
			}
		public string PressureTrend
			{
			get; set;
			}
		public double? RainRate
			{
			get; set;
			}
		public double? RainLast24Hours
			{
			get; set;
			}
		public string Description
			{
			get; set;
			}
		public string IconCode
			{
			get; set;
			}
		public string CloudDescription
			{
			get; set;
			}
		public string CloudIconCode
			{
			get; set;
			}
		public string LocationName
			{
			get; set;
			}
		public WeatherForecast Forecast
			{
			get; set;
			}
		public DateTime? ForecastUpdatedUtc
			{
			get; set;
			}
		public string SourceSummary
			{
			get; set;
			}
		public bool IsLocalCurrent
			{
			get; set;
			}
		}

	}

public static class SystemLocation
	{
	public static double Latitude => CrestronEnvironment.Latitude;
	public static double Longitude => CrestronEnvironment.Longitude;
	}
