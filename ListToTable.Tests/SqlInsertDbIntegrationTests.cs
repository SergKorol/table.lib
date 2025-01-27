using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using Dapper;

namespace ListToTable.Tests
{

    public class SqlInsertDbIntegrationTests : IClassFixture<DbFixture>, IDisposable
    {
        private readonly DbConnection _dbConnection;
        public SqlInsertDbIntegrationTests(DbFixture db)
        {
            _dbConnection = db.DbConnection;
            _dbConnection.Open();
        }
        
        public void Dispose()
        {
            _dbConnection.Dispose();
        }

        [Fact]
        public void TestGeneration()
        {
            IEnumerable<IDictionary<string, object>> table;
            var data = $"""SELECT Id, Name, Description, Price, PictureUrl, Type, Brand, QuantityInStock FROM Products;""";
            table = _dbConnection.Query(data) as IEnumerable<IDictionary<string, object>>;
            
            var collection = table as IDictionary<string, object>[] ?? (table ?? throw new InvalidOperationException()).ToArray();
            var queryString = DbTable.Add(collection, new Options
            {
                DateFormat = "dd-MM-yy",
                DecimalFormat = "#,##0.########"
            }).ToSqlInsertString().Trim();
            var lines = queryString.Split(Environment.NewLine);
            Assert.Equal("INSERT INTO Table1 (id,name,description,price,pictureurl,type,brand,quantityinstock) VALUES (1,'Angular Speedster Board 2000','Lorem ipsum dolor sit amet- consectetuer adipiscing elit. Maecenas porttitor congue massa. Fusce posuere- magna sed pulvinar ultricies- purus lectus malesuada libero- sit amet commodo magna eros quis urna.',20000,'/images/products/sb-ang1.png','Boards','Angular',100);", lines[0]);
        }
    }
}
