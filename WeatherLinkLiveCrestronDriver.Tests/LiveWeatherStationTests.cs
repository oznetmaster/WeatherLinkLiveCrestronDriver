// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License with Commons Clause. See LICENSE in the repository root.

using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;

using Crestron.DeviceDrivers.EntityModel;
using Crestron.DeviceDrivers.SDK;
using Crestron.DeviceDrivers.SDK.EntityModel;
using NUnit.Framework;
using WeatherlinkLive.CrestronDriver;

namespace WeatherLinkLiveCrestronDriver.Tests;

// Uses the real driver reader; each test owns a fresh entity and sends no station control commands.
[TestFixture, Category ("Processor"), Category ("Live"), NonParallelizable]
public sealed class LiveWeatherStationTests
	{
	private DriverLogger _logger;
	private WeatherStationDriver _driver;
	private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

	[SetUp]
	public void CreateConfiguredDriver ()
		{
		var parameter = TestContext.Parameters.Get ("EnableLiveTests", "");
		if (parameter.Length != 0 && !bool.TryParse (parameter, out _)) throw new InvalidDataException ("EnableLiveTests must be true or false.");
		if (parameter.Equals ("false", StringComparison.OrdinalIgnoreCase)) Assert.Ignore ("Live tests are disabled by EnableLiveTests=false.");
		var directory = TestContext.Parameters.Get ("TestDataDirectory", "");
		var path = Path.Combine (string.IsNullOrWhiteSpace (directory)
			? Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData), "WeatherLinkLive") : directory, "LiveTestSettings.json");
		LiveSettings settings = null;
		if (File.Exists (path))
			{
			using var stream = File.OpenRead (path);
			settings = (LiveSettings)new DataContractJsonSerializer (typeof (LiveSettings)).ReadObject (stream);
			}
		var enabled = parameter.Equals ("true", StringComparison.OrdinalIgnoreCase) || settings?.Enabled == true;
		if (!enabled) Assert.Ignore ("Live tests require private LiveTestSettings.json and explicit enablement.");
		var address = settings?.IpAddress;
		if (!IPAddress.TryParse (address, out var ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
			throw new InvalidDataException ("Enabled live tests require ipAddress in LiveTestSettings.json.");
#if NETFRAMEWORK
		if (Type.GetType ("Mono.Runtime") == null) Assert.Ignore ("Run live driver fixtures using the desktop SDK harness or processor runtime.");
#endif
		_logger = new DriverLogger ("weather-live-test");
		_driver = new WeatherStationDriver (new DriverControllerCreationArgs ("weather-live-test", TestSupport.DataDirectory, _logger.AppLogger, null), TestSupport.Resources (_logger), () => (0d, 0d));
		Set ("_weatherLinkLiveHost", address);
		Set ("_units", "metric");
		// Leave LocalWeatherReader unset so the real library/network path is exercised.
		}

	[TearDown]
	public void DisposeDriver () { _driver?.Dispose (); _logger?.Dispose (); }
	private void Set (string name, object value) => typeof (WeatherStationDriver).GetField (name, Private).SetValue (_driver, value);
	[DataContract]
	private sealed class LiveSettings
		{
		[DataMember (Name = "enabled")] public bool Enabled { get; set; }
		[DataMember (Name = "ipAddress")] public string IpAddress { get; set; }
		}
	private WeatherStationDriver.WeatherSnapshot Snapshot => (WeatherStationDriver.WeatherSnapshot)typeof (WeatherStationDriver).GetField ("_lastLocalWeatherSnapshot", Private).GetValue (_driver);
	private async Task Refresh ()
		{
		using var timeout = new CancellationTokenSource (TimeSpan.FromSeconds (30));
		await (Task)typeof (WeatherStationDriver).GetMethod ("RefreshLocalWeatherAsync", Private).Invoke (_driver, new object[] { timeout.Token });
		Assert.That (Snapshot, Is.Not.Null, "The real local station must return a snapshot; cloud fallback does not satisfy this test.");
		Assert.That (Snapshot.IsLocalCurrent, Is.True);
		Assert.That (_driver.OnlineIndicatorIsOnline && _driver.ReadyIndicatorIsReady, Is.True);
		}

	[Test]
	public async Task RealStation_RefreshPublishesMeasuredTemperatureAndHumidity ()
		{
		await Refresh ();
		Assert.That (Snapshot.Temperature, Is.InRange (-100d, 70d));
		Assert.That (Snapshot.Humidity, Is.InRange (0d, 100d));
		Assert.That (_driver.CurrentTemperatureDisplay, Does.EndWith ("°C"));
		Assert.That (_driver.TileDisplay, Does.Contain ("%"));
		Assert.That (_driver.HumiditySummary, Does.Contain ("%"));
		}

	[Test]
	public async Task RealStation_RepeatedRefreshReplacesSnapshotAndKeepsEntityReady ()
		{
		await Refresh ();
		var previous = Snapshot;
		await Refresh ();
		Assert.That (Snapshot, Is.Not.SameAs (previous));
		Assert.That (Snapshot.Pressure, Is.GreaterThan (0d));
		Assert.That (Snapshot.WindSpeed, Is.GreaterThanOrEqualTo (0d));
		}

	[Test]
	public async Task RealStation_UnitSelectionChangesReaderAndPublishedTemperature ()
		{
		await Refresh ();
		double celsius = Snapshot.Temperature.Value;
		Set ("_units", "imperial");
		await Refresh ();
		// Separate network snapshots may differ slightly as the station samples the weather.
		Assert.That (Snapshot.Temperature.Value, Is.EqualTo (celsius * 1.8 + 32).Within (1.0));
		Assert.That (_driver.CurrentTemperatureDisplay, Does.EndWith ("°F"));
		}
	}
