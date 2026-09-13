// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License with Commons Clause. See LICENSE in the repository root.

using System;
using System.Collections.Generic;
using System.Reflection;

using NUnit.Framework;

using Crestron.DeviceDrivers.EntityModel;
using Crestron.DeviceDrivers.EntityModel.Data;
using Crestron.DeviceDrivers.SDK;
using Crestron.DeviceDrivers.SDK.EntityModel;

using WeatherlinkLive.CrestronDriver;

namespace WeatherLinkLiveCrestronDriver.Tests;

[TestFixture, FixtureLifeCycle (LifeCycle.InstancePerTestCase), Category ("Processor")]
public sealed class ConfigurationLifecycleTests
	{
	private DriverLogger _logger;
	private WeatherStationDriver _driver;
	[SetUp]
	public void SetUp ()
		{
#if NETFRAMEWORK
		if (Type.GetType ("Mono.Runtime") == null)
			Assert.Ignore ("Requires the SDK desktop harness or the processor runtime.");
#endif
		_logger = new DriverLogger ("weather-configuration-test");
		var args = new DriverControllerCreationArgs ("weather-configuration-test", TestSupport.DataDirectory, _logger.AppLogger, null);
		_driver = new WeatherStationDriver (args, TestSupport.Resources (_logger), () => (56d, -5d));
		}
	[TearDown]
	public void TearDown ()
		{
		_driver?.Dispose ();
		_logger?.Dispose ();
		}
	private ConfigurationItemErrors Apply (Dictionary<string, DriverEntityValue?> values, bool clear = false)
		{
		return (ConfigurationItemErrors)typeof (WeatherStationDriver).GetMethod ("ApplyConfigurationItems", BindingFlags.Instance | BindingFlags.NonPublic).Invoke (
			_driver, new object[] { clear ? DataDrivenConfigurationController.ApplyConfigurationAction.ClearValues : DataDrivenConfigurationController.ApplyConfigurationAction.ApplyAll, null, values });
		}
	[TestCase ("LatitudeOverride", double.NaN)]
	[TestCase ("LatitudeOverride", double.PositiveInfinity)]
	[TestCase ("LatitudeOverride", 91d)]
	[TestCase ("LongitudeOverride", double.NaN)]
	[TestCase ("LongitudeOverride", -181d)]
	public void InvalidCoordinates_AreReportedWithoutChangingActiveLocation (string key, double value)
		{
		// Leave the API key absent so even a validation regression cannot make a cloud request.
		var values = new Dictionary<string, DriverEntityValue?> { ["LatitudeOverride"] = new DriverEntityValue (56d), ["LongitudeOverride"] = new DriverEntityValue (-5d) };
		values[key] = new DriverEntityValue (value);
		var errors = Apply (values);
		Assert.That (errors.ConfigurationErrorsByItemId.ContainsKey (key), Is.True);
		Assert.That (_driver.LocationSummary, Is.EqualTo ("56.0000, -5.0000"));
		}
	[TestCase ("LatitudeOverride")]
	[TestCase ("LongitudeOverride")]
	public void CoordinateOverrides_RequireBothValues (string key)
		{
		var errors = Apply (new Dictionary<string, DriverEntityValue?> { [key] = new DriverEntityValue (10d) });
		Assert.That (errors.ConfigurationErrorsByItemId.ContainsKey ("LatitudeOverride"), Is.True);
		Assert.That (errors.ConfigurationErrorsByItemId.ContainsKey ("LongitudeOverride"), Is.True);
		}
	[Test]
	public void InvalidConfiguration_DoesNotStartRefreshAndClearDiscardsPendingValues ()
		{
		var values = new Dictionary<string, DriverEntityValue?> { ["OpenWeatherApiKey"] = new DriverEntityValue ("synthetic-test-key"), ["Units"] = new DriverEntityValue ("unknown"), ["RefreshIntervalSeconds"] = new DriverEntityValue (1L) };
		var errors = Apply (values);
		Assert.That (errors.ConfigurationErrorsByItemId.ContainsKey ("Units"), Is.True);
		Assert.That (errors.ConfigurationErrorsByItemId.ContainsKey ("RefreshIntervalSeconds"), Is.True);
		Assert.That (_driver.GetState ().PropertyValues["onlineIndicator:isOnline"].GetValue<bool> (), Is.False);
		Assert.That (typeof (WeatherStationDriver).GetField ("_refreshCancellationTokenSource", BindingFlags.Instance | BindingFlags.NonPublic).GetValue (_driver), Is.Null);
		Assert.That (Apply (null, clear: true), Is.Null);
		var cleared = Apply (new Dictionary<string, DriverEntityValue?> ());
		Assert.That (cleared.ConfigurationErrorsByItemId.Keys, Is.EquivalentTo (new[] { "OpenWeatherApiKey" }));
		}
	}