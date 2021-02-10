using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Utils.Database
{
    public static class DbContextExtensions
    {    
        /// <summary>
        /// Adapted from https://stackoverflow.com/a/46013305
        /// </summary>
        /// <param name="database"></param>
        /// <param name="query"></param>
        /// <param name="map"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static List<T> MapRawSql<T>(this DatabaseFacade database, string query, Func<DbDataReader, T> map)
        {
            using (var command =  database.GetDbConnection().CreateCommand())
            {
                command.CommandText = query;
                command.CommandType = CommandType.Text;
                database.OpenConnection();
        
                using (var result = command.ExecuteReader())
                {
                    var entities = new List<T>();
                    while (result.Read())
                    {
                        entities.Add(map(result));
                    }
                    return entities;
                }
            }
        }
    }
}