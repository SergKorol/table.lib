using Dapper;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using table.runner;

namespace table.lib.tests
{
    public class SqlInsertTests : IClassFixture<DbFixture>, IDisposable
    {
        private readonly DbConnection _dbConnection;
        public SqlInsertTests(DbFixture db)
        {
            _dbConnection = db.DbConnection;
            _dbConnection.Open();
        }
        
        
        [Fact]
        public void TestGeneration()
        {
            var s = Table<TestClass>.Add(Samples.GetSampleOutput()).ToSqlInsertString();
            var lines = s.Split(Environment.NewLine);
            Assert.Equal("INSERT INTO TestClass (Field1,Field2,Field3,Field4,Field5,Field6) VALUES (321121,'Hi 312321',2121.32,1,'1970-01-01',34.43);", lines[0]);
            Assert.Equal("INSERT INTO TestClass (Field1,Field2,Field3,Field4,Field5,Field6) VALUES (32321,'Hi long text',21111111.32,1,'1970-01-01',34.43);", lines[1]);
            Assert.Equal("INSERT INTO TestClass (Field1,Field2,Field3,Field4,Field5,Field6) VALUES (321,'Hi longer text',2121.32,1,'1970-01-01',34.43);", lines[2]);
            Assert.Equal("INSERT INTO TestClass (Field1,Field2,Field3,Field4,Field5,Field6) VALUES (13,'Hi very long text',21111121.32,1,'1970-01-01',34.43);", lines[3]);
        }

        [Fact]
        public void TestNullGeneration()
        {
            var s = Table<TestClass>.Add(Samples.GetNullOutput()).ToSqlInsertString();
            var lines = s.Split(Environment.NewLine);
            Assert.Equal("INSERT INTO TestClass (Field1,Field2,Field3,Field4,Field5,Field6) VALUES (NULL,NULL,NULL,NULL,NULL,NULL);", lines[0]);
        }

        [Fact]
        public void TestNullFromDBGeneration()
        {
            IEnumerable<IDictionary<string, object>> table;
            const string data = $"""SELECT PhoneNumber, LockoutEnd FROM AspNetUsers""";
            table = _dbConnection.Query(data) as IEnumerable<IDictionary<string, object>>;

            var enumerable = table as IDictionary<string, object>[] ??
                             (table ?? throw new InvalidOperationException()).ToArray();


            var s = DbTable.Add(enumerable).ToSqlInsertString();
            var lines = s.Split(Environment.NewLine);
            Assert.Equal("INSERT INTO Table1 (phonenumber,lockoutend) VALUES (NULL,NULL);", lines[0]);
        }

        public void Dispose()
        {
            _dbConnection.Dispose();
        }
    }
}