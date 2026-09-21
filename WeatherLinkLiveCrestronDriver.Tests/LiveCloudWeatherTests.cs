// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License with Commons Clause. See LICENSE in the repository root.

using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;
using Crestron.DeviceDrivers.SDK;
using Crestron.DeviceDrivers.EntityModel;
using Crestron.DeviceDrivers.SDK.EntityModel;
using NUnit.Framework;
using WeatherlinkLive.CrestronDriver;

namespace WeatherLinkLiveCrestronDriver.Tests;

// Read-only cloud integration; no installed driver or physical station is modified.
[TestFixture, Category ("Processor"), Category ("Live"), Category ("LiveCloud"), NonParallelizable]
public sealed class LiveCloudWeatherTests
	{
	private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
	private DriverLogger _logger;
	private WeatherStationDriver _driver;

	[SetUp]
	public void CreateConfiguredDriver ()
		{
		if (!string.Equals (TestContext.Parameters.Get ("EnableLiveTests", "false"), "true", StringComparison.OrdinalIgnoreCase))
			Assert.Ignore ("Enable live tests and supply private CloudTestSettings.json to run cloud integration.");
#if NETFRAMEWORK
		if (Type.GetType ("Mono.Runtime") == null) Assert.Ignore ("Requires the desktop SDK harness or processor runtime.");
#endif
		string path = Path.Combine (TestContext.Parameters.Get ("TestDataDirectory", TestContext.CurrentContext.TestDirectory), "CloudTestSettings.json");
		CloudSettings settings;
		try
			{
			using var stream = File.OpenRead (path);
			settings = (CloudSettings)new DataContractJsonSerializer (typeof (CloudSettings)).ReadObject (stream);
			}
		catch { throw new InvalidDataException ("Enabled cloud tests require valid private CloudTestSettings.json."); }
		string apiKey = settings?.ApiKey;
		double? lat = settings?.Latitude, lon = settings?.Longitude;
		if (string.IsNullOrWhiteSpace (apiKey) || !lat.HasValue || !lon.HasValue || double.IsNaN (lat.Value) || double.IsNaN (lon.Value) || Math.Abs (lat.Value) > 90 || Math.Abs (lon.Value) > 180)
			throw new InvalidDataException ("Cloud settings require an API key and valid latitude/longitude.");
		_logger = new DriverLogger ("weather-cloud-live-test");
		_driver = new WeatherStationDriver (new DriverControllerCreationArgs ("weather-cloud-live-test", TestSupport.DataDirectory, _logger.AppLogger, null), TestSupport.Resources (_logger), () => (lat.Value, lon.Value));
		typeof (WeatherStationDriver).GetField ("_openWeatherApiKey", Private).SetValue (_driver, apiKey);
		_driver.LocalWeatherReader = ct => Task.FromResult<WeatherStationDriver.WeatherSnapshot> (null);
		}

	[TearDown]
	public void DisposeDriver () { _driver?.Dispose (); _logger?.Dispose (); }

	[Test]
	public async Task CloudOnlyRefreshPublishesCurrentAndDailyAndReusesCachedSnapshot ()
		{
		async Task Refresh ()
			{
			try
				{
				using var timeout = new CancellationTokenSource (TimeSpan.FromSeconds (45));
				await (Task)typeof (WeatherStationDriver).GetMethod ("RefreshWeatherAsync", Private).Invoke (_driver, new object[] { timeout.Token });
				}
			catch (Exception exception) { throw new InvalidOperationException ("Live cloud driver refresh failed (" + exception.GetType ().Name + "). Check connectivity and account access."); }
			}
		await Refresh ();
		var field = typeof (WeatherStationDriver).GetField ("_cloudWeatherSnapshot", Private);
		var snapshot = (WeatherStationDriver.CloudWeatherSnapshot)field.GetValue (_driver);
		string status = (string)typeof (WeatherStationDriver).GetField ("_lastStatus", Private).GetValue (_driver);
		string key = (string)typeof (WeatherStationDriver).GetField ("_openWeatherApiKey", Private).GetValue (_driver);
		Assert.That (snapshot, Is.Not.Null, "A live cloud snapshot must be retrieved. " + (status ?? string.Empty).Replace (key, "[redacted]"));
		Assert.That (snapshot.CurrentWeather.Main?.Temperature, Is.Not.Null);
		Assert.That (snapshot.Forecast.Daily, Is.Not.Empty);
		Assert.That (snapshot.Forecast.Hourly, Is.Empty, "The driver must not request unused hourly forecasts.");
		Assert.That (_driver.OnlineIndicatorIsOnline, Is.True);
		Assert.That (_driver.CurrentTemperatureDisplay, Is.Not.EqualTo ("--"));
		await Refresh ();
		Assert.That (field.GetValue (_driver), Is.SameAs (snapshot), "An immediate refresh must reuse the cloud snapshot.");
		}

	[DataContract]
	private sealed class CloudSettings
		{
		[DataMember (Name = "apiKey")] public string ApiKey { get; set; }
		[DataMember (Name = "latitude")] public double? Latitude { get; set; }
		[DataMember (Name = "longitude")] public double? Longitude { get; set; }
		}
	}
