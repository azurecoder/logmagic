﻿using LogMagic.Enrichers;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using NetBox.Extensions;
using System;
using System.Linq;

namespace LogMagic.Microsoft.Azure.ApplicationInsights.Writers
{
   class InsightsContext
   {
      private readonly TelemetryClient _client;
      private readonly WriterOptions _options;
      private readonly TelemetryContext _context;

      public InsightsContext(TelemetryClient client, WriterOptions options)
      {
         _client = client;
         _options = options;
         _context = client.Context;
      }

      public void Apply(LogEvent e)
      {
         LogMagicTelemetryProcessor.Version = e.UseProperty(KnownProperty.Version, string.Empty);
         LogMagicTelemetryProcessor.RoleName = e.UseProperty(KnownProperty.RoleName, string.Empty);
         LogMagicTelemetryProcessor.RoleInstance = e.UseProperty(KnownProperty.RoleInstance, string.Empty);

         switch(e.EventType)
         {
            case EventType.Dependency:
               ApplyDependency(e);
               break;

            case EventType.ApplicationEvent:
               ApplyEvent(e);
               break;

            case EventType.HandledRequest:
               ApplyRequest(e);
               break;

            case EventType.Metric:
               ApplyMetric(e);
               break;

            default:
               ApplyTrace(e);
               break;
         }

      }

      private void ApplyDependency(LogEvent e)
      {
         var d = new DependencyTelemetry()
         {
            Type = e.UseProperty<string>(KnownProperty.DependencyType),
            Name = e.UseProperty<string>(KnownProperty.DependencyName),
            Data = e.UseProperty<string>(KnownProperty.DependencyCommand),
            Target = e.UseProperty<string>(KnownProperty.DependencyTarget),
            Duration = TimeSpan.FromTicks(e.UseProperty<long>(KnownProperty.Duration)),
            Success = e.ErrorException == null,
         };
         string id = e.UseProperty<string>(KnownProperty.ActivityId);
         if (id != null) d.Id = id;
         AddProperties(d, e);
         Add(d, e);

         _client.TrackDependency(d);
      }

      private void ApplyEvent(LogEvent e)
      {
         var t = new EventTelemetry
         {
            Name = e.UseProperty<string>(KnownProperty.EventName)
         };
         Add(t, e);
         AddProperties(t, e);

         _client.TrackEvent(t);
      }

      private void ApplyTrace(LogEvent e)
      {
         var tr = new TraceTelemetry(e.FormattedMessage, GetSeverityLevel(e));
         Add(tr, e);
         AddProperties(tr, e);


         if (e.ErrorException != null)
         {
            if(_options.TraceExceptions)
            {
               _client.TrackTrace(tr);
            }

            var et = new ExceptionTelemetry(e.ErrorException);
            et.Message = e.FormattedMessage;

            Add(et, e);
            AddProperties(et, e);

            _client.TrackException(et);
         }
         else
         {
            _client.TrackTrace(tr);
         }
      }

      private void ApplyRequest(LogEvent e)
      {
         string name = e.UseProperty<string>(KnownProperty.RequestName);
         string uri = e.UseProperty<string>(KnownProperty.RequestUri);

         var tr = new RequestTelemetry
         {
            Name = name,
            Url = uri == null ? null : new Uri(uri),
            Duration = TimeSpan.FromTicks(e.UseProperty<long>(KnownProperty.Duration)),
            Success = e.ErrorException == null,
            ResponseCode = e.ErrorException == null ? "200" : e.ErrorException.GetType().Name
         };
         string id = e.UseProperty<string>(KnownProperty.ActivityId);
         if (id != null) tr.Id = id;
         Add(tr, e);
         AddProperties(tr, e);

         _client.TrackRequest(tr);
      }

      private void ApplyMetric(LogEvent e)
      {
         var t = new MetricTelemetry();
         t.Name = e.UseProperty<string>(KnownProperty.MetricName);
         t.Sum = e.UseProperty<double>(KnownProperty.MetricValue);
         Add(t, e);
         AddProperties(t, e);

         _client.TrackMetric(t);
      }

      //todo:
      //_client.TrackAvailability(null);
      //_client.TrackPageView(null);

      private static void AddProperties(ISupportProperties telemetry, LogEvent e)
      {
         telemetry.Properties.Add("loggerName", e.SourceName);

         if (e.Properties == null) return;

         telemetry.Properties.AddRange(e.Properties.ToDictionary(entry => entry.Key, entry => entry.Value?.ToString()));
      }

      private static void Add(ITelemetry telemetry, LogEvent e)
      {
         telemetry.Timestamp = e.EventTime;
         telemetry.Context.Operation.Id = e.UseProperty<string>(KnownProperty.OperationId);
         telemetry.Context.Operation.ParentId = e.UseProperty<string>(KnownProperty.ParentActivityId);
      }

      private static SeverityLevel GetSeverityLevel(LogEvent e)
      {
         LogSeverity sev = e.UseProperty(KnownProperty.Severity, LogSeverity.Information);

         switch (sev)
         {
            case LogSeverity.Verbose:
               return SeverityLevel.Verbose;
            case LogSeverity.Information:
               return SeverityLevel.Information;
            case LogSeverity.Warning:
               return SeverityLevel.Warning;
            case LogSeverity.Error:
               return SeverityLevel.Critical;
            case LogSeverity.Critical:
               return SeverityLevel.Critical;
            default:
               return SeverityLevel.Information;
         }
      }
   }
}
