﻿using LogMagic.Enrichers;
using Microsoft.ServiceFabric.Services.Remoting.V2;
using Microsoft.ServiceFabric.Services.Remoting.V2.Client;
using System;
using System.Fabric;
using System.Threading.Tasks;

namespace LogMagic.Microsoft.Azure.ServiceFabric.Remoting
{
   class CorrelatingServiceRemotingClient : IServiceRemotingClient
   {
      private static readonly ILog log = L.G(typeof(CorrelatingServiceRemotingClient));
      private readonly IServiceRemotingClient _inner;
      private readonly Action<CallSummary> _raiseSummary;
      private readonly string _remoteServiceName;
      private readonly RequestEnricher _enricher;

      public CorrelatingServiceRemotingClient(IServiceRemotingClient inner, Action<CallSummary> raiseSummary, string remoteServiceName)
      {
         _inner = inner;
         _raiseSummary = raiseSummary;
         _remoteServiceName = remoteServiceName;
         _enricher = new RequestEnricher();
      }

      public IServiceRemotingClient Inner => _inner;

      public ResolvedServicePartition ResolvedServicePartition
      {
         get => _inner.ResolvedServicePartition;
         set => _inner.ResolvedServicePartition = value;
      }

      public string ListenerName
      {
         get => _inner.ListenerName;
         set => _inner.ListenerName = value;
      }

      public ResolvedServiceEndpoint Endpoint
      {
         get => _inner.Endpoint;
         set => _inner.Endpoint = value;
      }

      public async Task<IServiceRemotingResponseMessage> RequestResponseAsync(IServiceRemotingRequestMessage requestMessage)
      {
         _enricher.Enrich(requestMessage);

         using (var time = new TimeMeasure())
         {
            string dependencyId = Guid.NewGuid().ToString();
            using (L.Context(KnownProperty.OperationParentId))
            {
               Exception gex = null;
               string methodName = MethodResolver.GetMethodName(requestMessage);
               try
               {
                  IServiceRemotingResponseMessage response = await _inner.RequestResponseAsync(requestMessage);

                  return response;
               }
               catch (Exception ex)
               {
                  gex = ex;
                  throw;
               }
               finally
               {
                  if (_raiseSummary != null)
                  {
                     var summary = new CallSummary(methodName, gex, time.ElapsedTicks);
                     _raiseSummary(summary);
                  }

                  log.Dependency(_remoteServiceName, _remoteServiceName, methodName, time.ElapsedTicks, gex,
                     KnownProperty.TelemetryId, dependencyId);
               }
            }
         }
      }

      public void SendOneWay(IServiceRemotingRequestMessage requestMessage)
      {
         _enricher.Enrich(requestMessage);

         _inner.SendOneWay(requestMessage);
      }
   }
}
