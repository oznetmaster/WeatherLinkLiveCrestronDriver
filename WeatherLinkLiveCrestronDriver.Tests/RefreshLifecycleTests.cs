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
		_driver.CloudWeatherReader = (lat, lon, ct) => { reads++; return Task.FromResult (new WeatherStationDriver.CloudWeatherSnapshot (null, null, DateTime.UtcNow, "Synthetic location")); };
		await TestSupport.Complete (Cloud ());
		object previous = Field ("_cloudWeatherSnapshot");
		await TestSupport.Complete (Cloud ());
		Assert.That (reads, Is.EqualTo (1));
		Assert.That (Field ("_cloudWeatherSnapshot"), Is.SameAs (previous));
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