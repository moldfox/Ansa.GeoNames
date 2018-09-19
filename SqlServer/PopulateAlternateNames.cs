﻿using System;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.IO;
using Microsoft.Extensions.Configuration;
using Ansa.Extensions;
using NGeoNames;

namespace Ansa.GeoNames.SqlServer
{
    public static class PopulateAlternateNames
    {
        public static void Populate(IConfiguration configuration)
        {
            Console.WriteLine("Getting ready to populate alternate names...");

            var connectionString = configuration["ConnectionString"];
            var dataPath = configuration["DataSourcePath"];
            var alternatesPath = Path.Combine(dataPath, @"alternateNamesV2.txt");
            var alternateLanguages = configuration["GeoNames:AlternateNamesLanguageCodes"] ?? String.Empty;
            var targetLanguages = alternateLanguages.Split(',');

            if (!File.Exists(alternatesPath))
            {
                Console.WriteLine("Downloading alternate names data...");
                var downloader = GeoFileDownloader.CreateGeoFileDownloader();
                downloader.DownloadFile("alternateNamesV2.zip", dataPath);
            }

            var results = GeoFileReader.ReadAlternateNamesV2(alternatesPath).OrderBy(p => p.Id);

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                Console.WriteLine("Populating alternate names...");

                var allowIdentityInsert = connection.CreateCommand();
                allowIdentityInsert.CommandText = @"SET IDENTITY_INSERT AlternateNames ON";

                try
                {
                    allowIdentityInsert.ExecuteNonQuery();
                }
                catch (SqlException exception)
                {
                    Console.WriteLine("SQL Exception occurred. Error Code: " + exception.ErrorCode);

                }

                const string sql = @"INSERT INTO AlternateNames (ID, GeoNameId, ISOLanguage, AlternateName, IsPreferredName, IsShortName, 
                        IsColloquial, IsHistoric, FromDate, ToDate)
                    VALUES (@ID, @GeoNameId, @ISOLanguage, @AlternateName, @IsPreferredName, @IsShortName, @IsColloquial, @IsHistoric, 
                        @FromDate, @ToDate)";

                var command = connection.CreateCommand();
                command.CommandText = sql;

                string[] parameterNames = new[]
                {
                    "@ID",
                    "@GeoNameId",
                    "@ISOLanguage",
                    "@AlternateName",
                    "@IsPreferredName",
                    "@IsShortName",
                    "@IsColloquial",
                    "@IsHistoric",
                    "@FromDate",
                    "@ToDate"
                };

                DbParameter[] parameters = parameterNames.Select(pn =>
                {
                    DbParameter parameter = command.CreateParameter();
                    parameter.ParameterName = pn;
                    command.Parameters.Add(parameter);
                    return parameter;
                })
                .ToArray();

                foreach (var r in results)
                {
                    if (targetLanguages.Count() > 0 && !targetLanguages.Contains(r.ISOLanguage))
                    {
                        continue;
                    }

                    parameters[0].Value = r.Id;
                    parameters[1].Value = r.GeoNameId;
                    parameters[2].Value = r.ISOLanguage.HasValueOrDBNull();
                    parameters[3].Value = r.Name.HasValueOrDBNull();
                    parameters[4].Value = r.IsPreferredName;
                    parameters[5].Value = r.IsShortName;
                    parameters[6].Value = r.IsColloquial;
                    parameters[7].Value = r.IsHistoric;
                    parameters[8].Value = r.From.HasValueOrDBNull();
                    parameters[9].Value = r.To.HasValueOrDBNull();

                    command.ExecuteNonQuery();

                    Console.WriteLine("Alternate Name ID: " + r.Id);
                }

                var disallowIdentityInsert = connection.CreateCommand();
                disallowIdentityInsert.CommandText = @"SET IDENTITY_INSERT AlternateNames OFF";

                try
                {
                    disallowIdentityInsert.ExecuteNonQuery();
                }
                catch (SqlException exception)
                {
                    Console.WriteLine("SQL Exception occurred. Error Code: " + exception.ErrorCode);

                }

                Console.WriteLine();
            }
        }
    }
}