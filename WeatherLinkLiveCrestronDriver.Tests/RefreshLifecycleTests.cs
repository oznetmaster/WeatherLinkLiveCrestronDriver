// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License with Commons Clause. See LICENSE in the repository root.

using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Crestron.DeviceDrivers.EntityModel;
using Crestron.DeviceDrivers.SDK;
using Crestron.DeviceDrivers.SDK.EntityModel;
using NUnit.Framework;
using WeatherlinkLive.CrestronDriver;

namespace WeatherLinkLiveCrestronDriver.Tests;

[TestFixture, Category ("Processor")]
public sealed class RefreshLifecycleTests
	{
	private DriverLogger _logger;
	private WeatherStationDriver _driver;
	private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
	[SetUp]
	public void SetUp ()
		{
#if NETFRAMEWORK
		if (Type.GetType ("Mono.Runtime") == null) Assert.Ignore ("Requires the SDK desktop harness or processor runtime.");
#endif
		_logger = new DriverLogger ("weather-refresh-test");
		_driver = new WeatherStationDriver (new DriverControllerCreationArgs ("weather-refresh-test", TestSupport.DataDirectory, _logger.AppLogger, null), TestSupport.Resources (_logger), () => (56d, -5d));
		Set ("_weatherLinkLiveHost", "192.0.2.1");
		_driver.LocalWeatherReader = ct => Task.FromResult (Snapshot ());
		_driver.CloudWeatherReader = (lat, lon, ct) => Task.FromResult (new WeatherStationDriver.CloudWeatherSnapshot (null, null, DateTime.UtcNow, "Synthetic location"));
		}
	[TearDown]
	public void TearDown () { _driver?.Dispose (); _logger?.Dispose (); }
	private void Set (string name, object value) => typeof (WeatherStationDriver).GetField (name, Private).SetValue (_driver, value);
	private object Field (string name) => typeof (WeatherStationDriver).GetField (name, Private).GetValue (_driver);
	private object Call (string name, params object[] args) => typeof (WeatherStationDriver).GetMethod (name, Private).Invoke (_driver, args);
	private Task Cloud () => (Task)Call ("GetRequestedCloudWeatherSnapshotAsync", 56d, -5d, true, CancellationToken.None);
	private static WeatherStationDriver.WeatherSnapshot Snapshot () => new () { Temperature = 18.5, Humidity = 65, IsLocalCurrent = true, SourceSummary = "Synthetic station" };
	private Task Refresh (bool localOnly) => (Task)Call (localOnly ? "RefreshLocalWeatherAsync" : "RefreshWeatherAsync", CancellationToken.None);
	private static WeatherStationDriver.CloudWeatherSnapshot CloudCurrent (double temperature = 16d) => new (null,
		new SimpleWeather.CurrentWeather ("{\"main\":{\"temp\":" + temperature.ToString (System.Globalization.CultureInfo.InvariantCulture) + ",\"pressure\":1015}}"), DateTime.UtcNow, "Synthetic location");
	private void RequestFreshCloud ()
		{
		// Simulate the next allowed attempt without waiting ten minutes on hardware.
		Set ("_lastCloudAttemptUtc", DateTime.UtcNow.AddMinutes (-11));
		Set ("_forceCloudRefresh", true);
		Set ("_forecastRequestPending", true);
		}
	[TestCase (false, false)]
	[TestCase (true, false)]
	[TestCase (false, true)]
	[TestCase (true, true)]
	public async Task NoCurrentSourceReportsOfflineAndRecovers (bool localOnly, bool cloudThrows)
		{
		_driver.LocalWeatherReader = ct => throw new InvalidOperationException ("Synthetic station unavailable");
		if (cloudThrows) _driver.CloudWeatherReader = (lat, lon, ct) => throw new InvalidOperationException ("Synthetic cloud unavailable");
		await TestSupport.Complete (Refresh (localOnly));
		Assert.That (_driver.OnlineIndicatorIsOnline, Is.False);
		Assert.That (_driver.ReadyIndicatorIsReady, Is.True);
		Assert.That (_driver.TileStatus, Is.EqualTo ("Weather unavailable"));
		Assert.That (_driver.CurrentTemperatureDisplay, Is.EqualTo ("--"));
		RequestFreshCloud ();
		_driver.CloudWeatherReader = (lat, lon, ct) => Task.FromResult (CloudCurrent ());
		await TestSupport.Complete (Refresh (localOnly));
		Assert.That (_driver.OnlineIndicatorIsOnline, Is.True);
		Assert.That (_driver.CurrentTemperatureDisplay, Does.Contain ("16.0"));
		}
	[TestCase (false, false)]
	[TestCase (true, false)]
	[TestCase (false, true)]
	[TestCase (true, true)]
	public async Task FailedSourcesRetainCachedLocalReadingsOfflineAndRecover (bool localOnly, bool cloudThrows)
		{
		await TestSupport.Complete (Refresh (localOnly));
		_driver.LocalWeatherReader = ct => throw new InvalidOperationException ("Synthetic station unavailable");
		if (cloudThrows) _driver.CloudWeatherReader = (lat, lon, ct) => throw new InvalidOperationException ("Synthetic cloud unavailable");
		RequestFreshCloud ();
		await TestSupport.Complete (Refresh (localOnly));
		Assert.That (_driver.OnlineIndicatorIsOnline, Is.False);
		Assert.That (_driver.CurrentTemperatureDisplay, Does.Contain ("18.5"));
		Assert.That (_driver.TileStatus, Does.EndWith (" failed"));
		_driver.LocalWeatherReader = ct => Task.FromResult (new WeatherStationDriver.WeatherSnapshot { Temperature = 19.5, IsLocalCurrent = true });
		await TestSupport.Complete (Refresh (localOnly));
		Assert.That (_driver.OnlineIndicatorIsOnline, Is.True);
		Assert.That (_driver.CurrentTemperatureDisplay, Does.Contain ("19.5"));
		Assert.That (_driver.TileStatus, Does.Not.EndWith (" failed"));
		}
	[TestCase (false, false)]
	[TestCase (true, false)]
	[TestCase (false, true)]
	[TestCase (true, true)]
	public async Task FailedCloudRequestRetainsHistoricalCloudReadingsOffline (bool localOnly, bool cloudThrows)
		{
		int reads = 0;
		_driver.LocalWeatherReader = ct => throw new InvalidOperationException ("Synthetic station unavailable");
		_driver.CloudWeatherReader = (lat, lon, ct) => { reads++; return Task.FromResult (CloudCurrent ()); };
		await TestSupport.Complete (Refresh (localOnly));
		RequestFreshCloud ();
		_driver.CloudWeatherReader = (lat, lon, ct) =>
			{
			reads++;
			if (cloudThrows) throw new InvalidOperationException ("Synthetic cloud unavailable");
			return Task.FromResult (new WeatherStationDriver.CloudWeatherSnapshot (null, null, DateTime.UtcNow, "Synthetic location"));
			};
		await TestSupport.Complete (Refresh (localOnly));
		Assert.That (reads, Is.EqualTo (2), "A real provider failure must be exercised rather than a throttled cache hit.");
		Assert.That (_driver.OnlineIndicatorIsOnline, Is.False);
		Assert.That (_driver.CurrentTemperatureDisplay, Does.Contain ("16.0"));
		Assert.That (_driver.TileStatus, Does.EndWith (" failed"));
		_driver.CloudWeatherReader = (lat, lon, ct) => { reads++; return Task.FromResult (CloudCurrent (17d)); };
		await TestSupport.Complete (Refresh (localOnly));
		Assert.That (reads, Is.EqualTo (2), "Failed attempts must remain throttled.");
		Assert.That (_driver.OnlineIndicatorIsOnline, Is.False, "Cached readings must not imply recovery before a successful retry.");
		RequestFreshCloud ();
		await TestSupport.Complete (Refresh (localOnly));
		Assert.That (reads, Is.EqualTo (3));
		Assert.That (_driver.OnlineIndicatorIsOnline, Is.True);
		Assert.That (_driver.CurrentTemperatureDisplay, Does.Contain ("17.0"));
		}
	[TestCase (false)]
	[TestCase (true)]
	public async Task IntentionalCloudCacheReuseRemainsOnline (bool localOnly)
		{
		int reads = 0;
		_driver.LocalWeatherReader = ct => throw new InvalidOperationException ("Synthetic station unavailable");
		_driver.CloudWeatherReader = (lat, lon, ct) => { reads++; return Task.FromResult (CloudCurrent ()); };
		await TestSupport.Complete (Refresh (localOnly));
		await TestSupport.Complete (Refresh (localOnly));
		Assert.That (reads, Is.EqualTo (1));
		Assert.That (_driver.OnlineIndicatorIsOnline, Is.True);
		}
	[Test]
	public async Task ForecastFailureDoesNotMakeFreshLocalCurrentOffline ()
		{
		RequestFreshCloud ();
		_driver.CloudWeatherReader = (lat, lon, ct) => throw new InvalidOperationException ("Synthetic forecast unavailable");
		await TestSupport.Complete (Refresh (false));
		Assert.That (_driver.OnlineIndicatorIsOnline, Is.True);
		Assert.That (_driver.CurrentTemperatureDisplay, Does.Contain ("18.5"));
		}
	[TestCase (false, false)]
	[TestCase (true, false)]
	[TestCase (false, true)]
	public async Task LateCloudFailureCannotChangeClearedState (bool localOnly, bool localHealthy)
		{
		var entered = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
		var release = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
		if (!localHealthy) _driver.LocalWeatherReader = ct => throw new InvalidOperationException ("Synthetic station unavailable");
		RequestFreshCloud ();
		_driver.CloudWeatherReader = async (lat, lon, ct) => { entered.TrySetResult (true); await release.Task; throw new InvalidOperationException ("Synthetic late cloud failure"); };
		Task refresh = Refresh (localOnly);
		string status = null;
		string tile = null;
		bool online = false;
		try
			{
			await TestSupport.Complete (entered.Task);
			Call ("ApplyConfigurationItems", DataDrivenConfigurationController.ApplyConfigurationAction.ClearValues, null, null);
			status = (string)Field ("_lastStatus");
			tile = _driver.TileStatus;
			online = _driver.OnlineIndicatorIsOnline;
			}
		finally { release.TrySetResult (true); }
		Assert.ThrowsAsync<OperationCanceledException> (async () => await TestSupport.Complete (refresh));
		Assert.That (Field ("_lastStatus"), Is.EqualTo (status));
		Assert.That (_driver.TileStatus, Is.EqualTo (tile));
		Assert.That (_driver.OnlineIndicatorIsOnline, Is.EqualTo (online));
		Assert.That (Field ("_cloudWeatherSnapshot"), Is.Null);
		}
	[TestCase ("metric", "Speed 36.0 kph", "Rate 25.4 mm/hr")]
	[TestCase ("uk", "Speed 22.4 mph", "Rate 25.4 mm/hr")]
	[TestCase ("imperial", "Speed 10.0 mph", "Rate 1.0 in/hr")]
	public void CloudFallback_ConvertsProviderWindAndRainUnitsBeforeDisplay (string units, string wind, string rain)
		{
		Set ("_units", units);
		var weather = new SimpleWeather.CurrentWeather ("{\"main\":{\"temp\":16,\"pressure\":1015.9166},\"wind\":{\"speed\":10},\"rain\":{\"1h\":25.4}}");
		var cloud = new WeatherStationDriver.CloudWeatherSnapshot (null, weather, DateTime.UtcNow, "Synthetic location");
		MethodInfo build = typeof (WeatherStationDriver).GetMethod ("BuildFallbackWeatherSnapshot", Private, null, new[] { typeof (WeatherStationDriver.CloudWeatherSnapshot) }, null);
		var snapshot = (WeatherStationDriver.WeatherSnapshot)build.Invoke (_driver, new object[] { cloud });
		Call ("ApplyWeatherState", snapshot, "Synthetic current conditions");
		Assert.That (_driver.WindSummary, Is.EqualTo (wind));
		Assert.That (_driver.RainRateSummary, Is.EqualTo (rain));
		}
	[Test]
	public async Task LocalReadPublishesMeasuredTemperatureAndCachesSnapshot ()
		{
		await TestSupport.Complete ((Task)Call ("RefreshLocalWeatherAsync", CancellationToken.None));
		Assert.That (_driver.CurrentTemperatureDisplay, Does.Contain ("18.5"));
		Assert.That (Field ("_lastLocalWeatherSnapshot"), Is.Not.Null);
		Assert.That (Field ("_lastLocalCurrentAvailable"), Is.EqualTo (true));
		}
	[Test]
	public async Task FailedLocalReadPreservesLastGoodCacheWithoutInventingNewMeasurements ()
		{
		await TestSupport.Complete ((Task)Call ("TryGetLocalCurrentWeatherAsync", CancellationToken.None));
		object previous = Field ("_lastLocalWeatherSnapshot");
		_driver.LocalWeatherReader = ct => throw new InvalidOperationException ("Synthetic station unavailable");
		await TestSupport.Complete ((Task)Call ("TryGetLocalCurrentWeatherAsync", CancellationToken.None));
		Assert.That (Field ("_lastLocalWeatherSnapshot"), Is.SameAs (previous));
		Assert.That (Field ("_lastLocalCurrentAvailable"), Is.EqualTo (false));
		}
	[Test]
	public async Task RepeatedCloudRequestWithinMinimumIntervalUsesCachedResult ()
		{
		int reads = 0;
		_driver.CloudWeatherReader = (lat, lon, ct) => { reads++; return Task.FromResult (CloudCurrent ()); };
		await TestSupport.Complete (Cloud ());
		object previous = Field ("_cloudWeatherSnapshot");
		await TestSupport.Complete (Cloud ());
		Assert.That (reads, Is.EqualTo (1));
		Assert.That (Field ("_cloudWeatherSnapshot"), Is.SameAs (previous));
		}
	[TestCase (false)]
	[TestCase (true)]
	public async Task FailedCloudAttemptIsThrottledWithOrWithoutCachedWeather (bool cached)
		{
		if (cached) Set ("_cloudWeatherSnapshot", new WeatherStationDriver.CloudWeatherSnapshot (null, CloudCurrent ().CurrentWeather, DateTime.UtcNow.AddMinutes (-20), "Cached"));
		Set ("_forecastRequestPending", true);
		int reads = 0;
		_driver.CloudWeatherReader = (lat, lon, ct) => { reads++; throw new InvalidOperationException ("Synthetic cloud failure"); };
		Assert.ThrowsAsync<InvalidOperationException> (async () => await Cloud ());
		object previous = Field ("_cloudWeatherSnapshot");
		Assert.ThrowsAsync<InvalidOperationException> (async () => await Cloud ());
		Assert.ThrowsAsync<InvalidOperationException> (async () => await Cloud ());
		Assert.That (reads, Is.EqualTo (1), "Neither local polls nor repeated commands should repeat a failed cloud request immediately.");
		Assert.That (Field ("_cloudWeatherSnapshot"), Is.SameAs (previous));
		Assert.That (Field ("_forecastRequestPending"), Is.True, "Retry must remain pending for the next allowed interval.");
		Set ("_lastCloudAttemptUtc", DateTime.UtcNow.AddMinutes (-11));
		_driver.CloudWeatherReader = (lat, lon, ct) => { reads++; return Task.FromResult (CloudCurrent ()); };
		await Cloud ();
		Assert.That (reads, Is.EqualTo (2));
		Assert.That (Field ("_forecastRequestPending"), Is.False);
		}

	[Test]
	public async Task CloudBackoffDoesNotPreventLocalStationRefresh ()
		{
		Set ("_lastCloudAttemptUtc", DateTime.UtcNow);
		Set ("_forecastRequestPending", true);
		int cloudReads = 0;
		_driver.CloudWeatherReader = (lat, lon, ct) => { cloudReads++; throw new InvalidOperationException ("Must remain throttled"); };
		await Refresh (false);
		Assert.That (cloudReads, Is.Zero);
		Assert.That (_driver.CurrentTemperatureDisplay, Does.Contain ("18.5"));
		Assert.That (_driver.OnlineIndicatorIsOnline, Is.True);
		}

	[TestCase (false, false)]
	[TestCase (true, false)]
	[TestCase (false, true)]
	[TestCase (true, true)]
	public async Task LateWeatherCannotRestoreCacheOrDisplayAfterClearOrDispose (bool cloud, bool dispose)
		{
		var entered = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
		var release = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
		_driver.LocalWeatherReader = async ct => { entered.TrySetResult (true); await release.Task; return Snapshot (); };
		_driver.CloudWeatherReader = async (lat, lon, ct) => { entered.TrySetResult (true); await release.Task; return new WeatherStationDriver.CloudWeatherSnapshot (null, null, DateTime.UtcNow, "Late location"); };
		var refresh = cloud ? Cloud () : (Task)Call ("RefreshLocalWeatherAsync", CancellationToken.None);
		try
			{
			await TestSupport.Complete (entered.Task);
			if (dispose) _driver.Dispose ();
			else Call ("ApplyConfigurationItems", DataDrivenConfigurationController.ApplyConfigurationAction.ClearValues, null, null);
			}
		finally { release.TrySetResult (true); }
		string display = _driver.CurrentTemperatureDisplay;
		try { await TestSupport.Complete (refresh); } catch (OperationCanceledException) { }
		Assert.That (Field ("_lastLocalWeatherSnapshot"), Is.Null);
		Assert.That (Field ("_cloudWeatherSnapshot"), Is.Null);
		Assert.That (_driver.CurrentTemperatureDisplay, Is.EqualTo (display));
		}
	}
