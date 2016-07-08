﻿using System;
using System.Collections.Generic;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json.Linq;

namespace LogMagic.WindowsAzure
{
   /// <summary>
   /// Azure Table Storage receiver
   /// </summary>
   class AzureTableLogWriter : ILogWriter
   {
      private readonly CloudTable _table;

      /// <summary>
      /// Creates class instance
      /// </summary>
      /// <param name="storageAccountName">Storage account name</param>
      /// <param name="storageAccountKey">Storage account key</param>
      /// <param name="tableName">Target table name</param>
      public AzureTableLogWriter(string storageAccountName, string storageAccountKey, string tableName)
      {
         var creds = new StorageCredentials(storageAccountName, storageAccountKey);
         var account = new CloudStorageAccount(creds, true);

         CloudTableClient tableClient = account.CreateCloudTableClient();
         _table = tableClient.GetTableReference(tableName);
         _table.CreateIfNotExists();
      }

      /// <summary>
      /// Sends chunks to table
      /// </summary>
      /// <param name="events"></param>
      public void Write(IEnumerable<LogEvent> events)
      {
         var batch = new TableBatchOperation();

         foreach (LogEvent e in events)
         {
            var row = new ElasticTableEntity
            {
               PartitionKey = e.EventTime.ToString("yy-MM-dd"),
               RowKey = e.EventTime.ToString("HH-mm-ss-fff")
            };

            row.Add("source", e.SourceName);
            row.Add("severity", e.Severity);
            row.Add("message", e.Message);
            row.Add("error", e.ErrorException == null ? string.Empty : e.ErrorException.ToString());

            if (e.Properties != null)
            {
               foreach (var p in e.Properties)
               {
                  if (p.Key == LogEvent.ErrorPropertyName) continue;

                  row.Add(p.Key, p.Value);
               }
            }

            batch.Insert(row);
         }

         if (batch.Count > 0)
         {
            _table.ExecuteBatch(batch);
         }
      }

      public void Dispose()
      {
      }
   }
}
