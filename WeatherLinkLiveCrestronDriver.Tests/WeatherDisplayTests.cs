// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License with Commons Clause. See LICENSE in the repository root.

using System;

using NUnit.Framework;

using WeatherlinkLive.CrestronDriver;
namespace WeatherLinkLiveCrestronDriver.Tests;

[TestFixture, FixtureLifeCycle (LifeCycle.InstancePerTestCase), SetCulture ("fr-FR")]
public sealed class WeatherDisplayTests
	{
	private static T Call<T> (string name, params object[] args) => TestSupport.Call<T> (typeof (WeatherStationDriver), name, args);
	[TestCase (null, "metric")]
	[TestCase ("", "metric")]
	[TestCase ("Metric", "metric")]
	[TestCase ("UK", "uk")]
	[TestCase ("IMPERIAL", "imperial")]
	[TestCase ("bad", null)]
	public void Units_NormalizeSupportedChoices (string units, string expected) => Assert.That (Call<string> ("NormalizeUnits", units), Is.EqualTo (expected));
	[TestCase (null, true, "--")]
	[TestCase (null, false, "--")]
	[TestCase (12.25, true, "12.3°C")]
	[TestCase (-2.25, false, "-2.3°F")]
	public void Temperature_UsesInvariantDecimalsAndCorrectUnits (double? value, bool metric, string expected) => Assert.That (Call<string> ("FormatTemperature", value, metric), Is.EqualTo (expected));
	[TestCase (null, true, "--")]
	[TestCase (2.5, true, "3°C")]
	[TestCase (-2.5, true, "-3°C")]
	[TestCase (72.5, false, "73°F")]
	public void TileTemperature_RoundsMidpointsAwayFromZero (double? value, bool metric, string expected) => Assert.That (Call<string> ("FormatRoundedTemperature", value, metric), Is.EqualTo (expected));
	[TestCase (null, true, false)]
	[TestCase (-0.1, true, true)]
	[TestCase (0d, true, false)]
	[TestCase (31.9, false, true)]
	[TestCase (32d, false, false)]
	public void FreezingThreshold_UsesSelectedTemperatureUnits (double? value, bool metric, bool expected) => Assert.That (Call<bool> ("IsBelowFreezing", value, metric), Is.EqualTo (expected));
	[TestCase ("01d", "icSun")]
	[TestCase ("01n", "icSun")]
	[TestCase ("02d", "icSmallSun")]
	[TestCase ("04n", "icSmallSun")]
	[TestCase ("50d", "icSmallSun")]
	[TestCase ("09d", "icHumidifying")]
	[TestCase ("10n", "icHumidifying")]
	[TestCase ("11d", "icQuickAction")]
	[TestCase ("13d", "icCoolingRegular")]
	public void CloudConditions_MapToSupportedCrestronIcons (string code, string expected) => Assert.That (Call<string> ("MapCloudWeatherIcon", code, null, false), Is.EqualTo (expected));
	[TestCase ("09d")]
	[TestCase ("10n")]
	[TestCase (null)]
	public void DryLocalObservation_SuppressesCloudRainIcon (string code) => Assert.That (Call<string> ("MapCloudWeatherIcon", code, "Light rain", true), Is.EqualTo ("icSmallSun"));
	[TestCase ("Thunderstorm", "icQuickAction")]
	[TestCase ("Snow", "icCoolingRegular")]
	[TestCase ("Light rain", "icHumidifying")]
	[TestCase ("Gusty winds", "icFanOn")]
	[TestCase ("Fog", "icSmallSun")]
	[TestCase ("Clear sky", "icSun")]
	[TestCase (null, "icSmallSun")]
	[TestCase ("Unknown condition", "icClimateRegular")]
	public void UnknownCloudCodes_FallBackToDescription (string description, string expected) => Assert.That (Call<string> ("MapCloudWeatherIcon", "unknown", description, false), Is.EqualTo (expected));
	[Test]
	public void WeatherDescription_SupportsObjectCollectionsAndMissingData ()
		{
		var first = new DescriptionData { Description = "Light rain", Main = "Rain" };
		Assert.That (Call<string> ("GetPrimaryWeatherDescription", first), Is.EqualTo ("Light rain"));
		Assert.That (Call<string> ("GetPrimaryWeatherDescription", (object)new[] { first, new DescriptionData { Description = "Clouds" } }), Is.EqualTo ("Light rain"));
		first.Description = " ";
		Assert.That (Call<string> ("GetPrimaryWeatherDescription", first), Is.EqualTo ("Rain"));
		Assert.That (Call<string> ("GetPrimaryWeatherDescription", (object)Array.Empty<DescriptionData> ()), Is.EqualTo ("Unknown conditions"));
		Assert.That (Call<string> ("GetPrimaryWeatherDescription", (object)null), Is.EqualTo ("Unknown conditions"));
		}
	public sealed class DescriptionData
		{
		public string Description
			{
			get; set;
			}
		public string Main
			{
			get; set;
			}
		}
	}