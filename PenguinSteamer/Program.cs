using System;
using Penguinium;
using Chickenium;
using Microsoft.EntityFrameworkCore;
using Chickenium.Dao;
using System.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PenguinSteamer
{
    // TODO:AppConfigにあかんものが書かれているので、必ず外出しすること。

    class Program
    {
        static void Main(string[] args)
        {
            // 設定
            const string ConfigFileName = "App.Config.json";
            const string ConfigClassName = "appSettings";
            const string ConfigFieldName = "bot_setting";

            // DB接続
            var dbContextOptions = SqlServerConnector.GetDbContextOptions(ConfigFileName, ConfigClassName, ConfigFieldName, new AppLoggerProvider());

            Logger.Log("BOTを起動します");
            BotLogic bot = new BotLogic(dbContextOptions);
        }
    }
}
