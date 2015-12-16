﻿using System;
using System.Threading;
using Config.Net;
using Config.Net.Stores;
using LogMagic.WindowsAzure;
using NUnit.Framework;
using System.IO;
using LogMagic.Receivers;

namespace LogMagic.Test
{
   /// <summary>
   /// Only tests that logging on all the provider doesn't crash"
   /// </summary>
   [TestFixture("azure-blob")]
   [TestFixture("azure-table")]
   [TestFixture("files")]
   public class SmokeAndMirrorsLogging : AbstractTestFixture
   {
      private static readonly Setting<string> AzureStorageName = new Setting<string>("Azure.Storage.Name", null);
      private static readonly Setting<string> AzureStorageKey = new Setting<string>("Azure.Storage.Key", null);

      private readonly string _receiverName;

      public SmokeAndMirrorsLogging(string receiverName)
      {
         _receiverName = receiverName;
      }

      [SetUp]
      public void SetUp()
      {
         L.ClearReceivers();
         Cfg.Configuration.RemoveAllStores();
         Cfg.Configuration.AddStore(new IniFileConfigStore("c:\\tmp\\integration-tests.ini"));
         L.NodeId = "testnode";

         switch (_receiverName)
         {
            case "azure-blob":
               L.AddReceiver(new AzureAppendBlobLogReceiver(Cfg.Read(AzureStorageName), Cfg.Read(AzureStorageKey),
                  "logs-integration", "smokeandmirrors"));
               break;
            case "azure-table":
               L.AddReceiver(new AzureTableLogReceiver(Cfg.Read(AzureStorageName), Cfg.Read(AzureStorageKey),
                  "logsintegration"));
               break;
            case "files":
               L.AddReceiver(new FileReceiver(Path.Combine(TestDir.FullName, "testlog.txt")));
               break;
         }   
      }

      [TearDown]
      public void TearDown()
      {
         
      }

      [Test]
      public void Smoke_SomethingSimple_DoestCrash()
      {
         ILog log = L.G();

         log.D("hello!");

         Thread.Sleep(TimeSpan.FromSeconds(5));
      }
   }
}