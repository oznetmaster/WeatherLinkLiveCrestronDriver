// Copyright (c) 2026 Neil Colvin. MIT with Commons Clause; see LICENSE.
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Crestron.DeviceDrivers.SDK;
using Crestron.DeviceDrivers.EntityModel;
using Crestron.DeviceDrivers.SDK.EntityModel;
using Crestron.DeviceDrivers.SDK.EntityModel.Attributes;
using NUnit.Framework;
using WeatherlinkLive.CrestronDriver;
using Client = WeatherLinkLive.WeatherLinkLiveAPI.WeatherLinkLive;

namespace WeatherLinkLiveCrestronDriver.Tests;

[TestFixture]
public sealed class LocalRecoveryTests
	{
	private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
	private sealed class WeatherHttp : HttpMessageHandler
		{
		internal bool Offline;
		internal int Requests;
		protected override Task<HttpResponseMessage> SendAsync (HttpRequestMessage request, CancellationToken token)
			{
			Requests++;
			return Offline ? Task.FromException<HttpResponseMessage> (new HttpRequestException ("Synthetic outage")) :
				Task.FromResult (new HttpResponseMessage (HttpStatusCode.OK) { Content = new StringContent ("{\"data\":{\"conditions\":[{\"data_structure_type\":1,\"temp\":68,\"hum\":55}]},\"error\":null}") });
			}
		}
	private static Task Refresh (WeatherStationDriver driver) => (Task)typeof (WeatherStationDriver).GetMethod ("RefreshWeatherAsync", Private).Invoke (driver, new object[] { CancellationToken.None });

	[TestCase (false)]
	[TestCase (true)]
	public async Task PersistentClient_RecoversAndPublishesProgrammableEventsWithoutDriverPolling (bool slowCloud)
		{
#if NETFRAMEWORK
		if (Type.GetType ("Mono.Runtime") == null) Assert.Ignore ("Requires desktop SDK harness or processor runtime.");
#endif
		using var logger = new DriverLogger ("weather-recovery-test");
		using var driver = new WeatherStationDriver (new DriverControllerCreationArgs ("weather-recovery-test", TestSupport.DataDirectory, logger.AppLogger, null), TestSupport.Resources (logger), () => (56d, -5d));
		var http = new WeatherHttp ();
		DateTime now = DateTime.UtcNow;
		var client = (Client)typeof (Client).GetConstructors (Private).Single ().Invoke (new object[] { IPAddress.Parse ("192.0.2.10"), http, (Func<DateTime>)(() => now), 10, 120, false, false, false, false, null });
		var delayed = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
		var release = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
		typeof (Client).GetProperty ("RecoveryDelayAsync", Private).SetValue (client, (Func<TimeSpan, CancellationToken, Task>)(async (delay, token) =>
			{
			using var registration = token.Register (() => release.TrySetCanceled ());
			delayed.TrySetResult (true);
			await release.Task;
			}));
		int created = 0, disconnected = 0, reconnected = 0;
		driver.LocalWeatherClientFactory = () => { created++; return client; };
		typeof (WeatherStationDriver).GetField ("_weatherLinkLiveHost", Private).SetValue (driver, "192.0.2.10");
		driver.CloudWeatherReader = (_, _, _) => Task.FromResult (new WeatherStationDriver.CloudWeatherSnapshot (null, null, now, "Synthetic"));
		driver.WeatherLinkDisconnected += (_, _) => disconnected++;
		driver.WeatherLinkReconnected += (_, _) => reconnected++;
		await Refresh (driver);
		typeof (WeatherStationDriver).GetField ("_units", Private).SetValue (driver, "metric");
		var metric = (WeatherStationDriver.WeatherSnapshot)typeof (WeatherStationDriver).GetMethod ("ReadLocalSnapshot", Private).Invoke (driver, new object[] { client });
		Assert.That (metric.Temperature, Is.EqualTo (20).Within (0.01));
		Assert.That (client.MetricRain && client.MetricWind && client.MetricBarometer, Is.True);
		typeof (WeatherStationDriver).GetField ("_units", Private).SetValue (driver, "imperial");
		var imperial = (WeatherStationDriver.WeatherSnapshot)typeof (WeatherStationDriver).GetMethod ("ReadLocalSnapshot", Private).Invoke (driver, new object[] { client });
		Assert.That (imperial.Temperature, Is.EqualTo (68).Within (0.01));
		Assert.That (client.MetricRain || client.MetricWind || client.MetricBarometer, Is.False);
		var cloud = new TaskCompletionSource<WeatherStationDriver.CloudWeatherSnapshot> (TaskCreationOptions.RunContinuationsAsynchronously);
		if (slowCloud)
			{
			driver.CloudWeatherReader = (_, _, _) => cloud.Task;
			typeof (WeatherStationDriver).GetField ("_cloudWeatherSnapshot", Private).SetValue (driver, null);
			typeof (WeatherStationDriver).GetField ("_lastCloudAttemptUtc", Private).SetValue (driver, DateTime.MinValue);
			}
		now = now.AddSeconds (10);
		http.Offline = true;
		Task failedRefresh = Refresh (driver);
		Assert.That (await Task.WhenAny (delayed.Task, Task.Delay (5000)), Is.SameAs (delayed.Task));
		await Refresh (driver);
		Assert.That (http.Requests, Is.EqualTo (2), "Driver must not bypass the client's backoff");
		Assert.That (disconnected, Is.EqualTo (1));
		http.Offline = false;
		now = now.AddSeconds (10);
		Task recovery = (Task)typeof (Client).GetProperty ("RecoveryCompletion", Private).GetValue (client);
		release.TrySetResult (true);
		Assert.That (await Task.WhenAny (recovery, Task.Delay (5000)), Is.SameAs (recovery));
		await recovery;
		cloud.TrySetResult (new WeatherStationDriver.CloudWeatherSnapshot (null, null, now, "Synthetic"));
		await failedRefresh;
		Assert.That (created, Is.EqualTo (1));
		Assert.That (reconnected, Is.EqualTo (1));
		Assert.That (driver.OnlineIndicatorIsOnline, Is.True);
		Assert.That (driver.SourceSummary, Does.Contain ("WeatherLink Live"));
		typeof (WeatherStationDriver).GetMethod ("StopRefreshLoop", Private).Invoke (driver, null);
		typeof (WeatherStationDriver).GetMethod ("OnLocalWeatherDisconnected", Private).Invoke (driver, new object[] { client, EventArgs.Empty });
		typeof (WeatherStationDriver).GetMethod ("OnLocalWeatherReconnected", Private).Invoke (driver, new object[] { client, EventArgs.Empty });
		Assert.That (disconnected, Is.EqualTo (1), "Retired clients must not publish events");
		Assert.That (reconnected, Is.EqualTo (1));
		}

	[TestCase ("WeatherLinkDisconnected")]
	[TestCase ("WeatherLinkReconnected")]
	public void ConnectionEvents_AreProgrammable (string name)
		{
		var eventInfo = typeof (WeatherStationDriver).GetEvent (name);
		Assert.That (eventInfo.GetCustomAttribute<EntityEventAttribute> (), Is.Not.Null);
		Assert.That (eventInfo.GetCustomAttribute<EntityEventMetadataAttribute> ().Programmable, Is.True);
		}
	}
