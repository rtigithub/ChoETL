﻿using ChoETL;
using System;
using System.Data;
using System.Text;
using System.Linq;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Globalization;
using System.Threading;

namespace ChoParquetReaderTest
{
    class Program
    {
        static void Test1()
        {
            StringBuilder csv = new StringBuilder();
            using (var r = new ChoParquetReader(@"test1.parquet")
                .ParquetOptions(o => o.TreatByteArrayAsString = true))
            {
                using (var w = new ChoCSVWriter(csv)
                    .WithFirstLineHeader()
                    .UseNestedKeyFormat(false)
                    )
                    w.Write(r);
            }

            Console.WriteLine(csv.ToString());
        }
        static void DataTableTest()
        {
            StringBuilder csv = new StringBuilder();
            using (var r = new ChoParquetReader(@"test1.parquet")
                .ParquetOptions(o => o.TreatByteArrayAsString = true))
            {
                var dt = r.AsDataTable();
            }

            Console.WriteLine(csv.ToString());
        }

        static void ByteArrayTest()
        {
            StringBuilder csv = new StringBuilder();
            using (var r = new ChoParquetReader(@"ByteArrayTest.parquet")
                .ParquetOptions(o => o.TreatByteArrayAsString = true)
                )
            {
                var dt = r.AsDataTable("x");
                Console.WriteLine(ChoJSONWriter.ToText(dt));
                return;
                using (var w = new ChoCSVWriter(csv)
                    .WithFirstLineHeader()
                    .UseNestedKeyFormat(false)
                    )
                    w.Write(r);
            }

            Console.WriteLine(csv.ToString());
        }

        static void ReadParquet52()
        {
            using (var r = new ChoParquetReader("myData52.parquet"))
            {
                foreach (var rec in r.Take(1))
                    Console.WriteLine(rec.Dump());
            }
        }

        static void CSV2ParquetTest()
        {
            using (var r = new ChoCSVReader(@"..\..\..\..\..\..\data\XBTUSD.csv")
                .Configure(c => c.LiteParsing = true)
                .NotifyAfter(100000)
                .OnRowsLoaded((o, e) => $"Rows Loaded: {e.RowsLoaded} <-- {DateTime.Now}".Print())
                .ThrowAndStopOnMissingField(false)
                )
            {
                //r.Loop();
                //return;
                using (var w = new ChoParquetWriter(@"..\..\..\..\..\..\data\XBTUSD.parquet")
                    .Configure(c => c.RowGroupSize = 100000)
                .Configure(c => c.LiteParsing = true)
                    )
                    w.Write(r);
            }
        }

        public class Trade
        {
            public long? Id { get; set; }
            public double? Price { get; set; }
            public double? Quantity { get; set; }
            public DateTime? CreateDateTime{ get; set; }
            public bool? IsActive { get; set; }
            public Decimal? Total { get; set; }
        }

        static void WriteParquetWithNullableFields()
        {
            ChoTypeConverterFormatSpec.Instance.DateTimeFormat = "MM^dd^yyyy";
            //Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("fr-FR");
            using (var w = new ChoParquetWriter<Trade>(@"C:\Temp\Trade1.parquet")
                )
            {
                w.Write(new Trade
                {
                    Id = 1,
                    Price = 1.3,
                    Quantity = 2.45,
                    CreateDateTime = null,
                });
            }

            using (var r = new ChoParquetReader<Trade>(@"C:\Temp\Trade1.parquet"))
            {
                var rec = r.First();
                rec.Print();
            }
        }
        static void ParseLargeParquetTest()
        {
            using (var r = new ChoParquetReader(@"..\..\..\..\..\..\data\XBTUSD-Copy.parquet")
                .Configure(c => c.LiteParsing = true)
                .NotifyAfter(100000)
                .OnRowsLoaded((o, e) => $"Rows Loaded: {e.RowsLoaded} <-- {DateTime.Now}".Print())
                .ThrowAndStopOnMissingField(false)
                .Setup(s => s.BeforeRowGroupLoad += (o, e) => e.Skip = e.RowGroupIndex < 2)
                )
            {
                r.Loop();
            }

        }

        static void DB2ParquetTest()
        {
            using (var conn = new SqlConnection(@"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=C:\Projects\GitHub\ChoETL\src\Test\ChoETL.SqlServer.Core.Test\bin\Debug\net5.0\localdb.mdf;Integrated Security=True;Connect Timeout=30"))
            {
                conn.Open();
                var cmd = new SqlCommand("SELECT * FROM Trade", conn);

                var dr = cmd.ExecuteReader();

                using (var w = new ChoParquetWriter<Trade>(@"C:\Temp\Trade.parquet")
                    //.Configure(c => c.LiteParsing = true)
                    .Configure(c => c.RowGroupSize = 5000)
                    .NotifyAfter(100000)
                    .OnRowsWritten((o, e) => $"Rows Loaded: {e.RowsWritten} <-- {DateTime.Now}".Print())
                    .ThrowAndStopOnMissingField(false)
                    )
                {
                    w.Write(dr);
                }


            }
        }

        static void Issue144()
        {
            string connectionString = @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=C:\Temp\Northwind.MDF;Integrated Security=True;Connect Timeout=30";
            SqlConnection conn = new SqlConnection(connectionString);
            conn.Open();

            SqlCommand command = new SqlCommand("SELECT * FROM Employees", conn);
            using (var r = command.ExecuteReader(CommandBehavior.CloseConnection))
            {
                using (var parser = new ChoParquetWriter(@"C:\temp\emp.parquet")
            .Configure(c => c.CompressionMethod = Parquet.CompressionMethod.Gzip)
            .Configure(c => c.RowGroupSize = 1000)
            .NotifyAfter(1000)
            .OnRowsWritten((o, e) => $"Rows: {e.RowsWritten} <--- {DateTime.Now}".Print()))
                {
                    if (r.HasRows)
                    {
                        parser.Write(r);
                    }
                }
            }


        }
        static void Main(string[] args)
        {
            ChoETLFrxBootstrap.TraceLevel = System.Diagnostics.TraceLevel.Error;
            WriteParquetWithNullableFields();
            return;
            Issue144();
            return;
            WriteParquetWithNullableFields();
        }

        static void Issue233()
        {
            using (var r = new ChoParquetReader(@"C:\Users\nraj39\Downloads\ships.parquet")
                .IgnoreField("DataSetVersion1")
                .ParquetOptions(o => o.TreatByteArrayAsString = true)
                .ErrorMode(ChoErrorMode.ThrowAndStop)
                )
            {
                var rec = r.First();
                rec.Print();
            }
        }

        static void MissingFieldValueTest()
        {
            string csv = @"Id,Name
1,
2,Carl
3,Mark";
            string parquetFilePath = "missingfieldvalue.parquet";
            CreateParquetFile(parquetFilePath, csv);

            foreach (dynamic e in new ChoParquetReader(parquetFilePath))
            {
                Console.WriteLine(e.Id);
                Console.WriteLine(e.Name);
            }
        }

        static void CreateParquetFile(string parquetFilePath, string csv)
        {
            using (var r = ChoCSVReader.LoadText(csv)
                   .WithFirstLineHeader()
                  )
            {
                using (var w = new ChoParquetWriter(parquetFilePath))
                    w.Write(r);
            }
        }
    }
}
