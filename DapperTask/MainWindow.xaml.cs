﻿using Dapper;
using DapperTask.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Z.Dapper.Plus;

namespace DapperTask;

public partial class MainWindow : Window,INotifyPropertyChanged
{
    private SqlConnection? _connection;
    private IConfigurationRoot? _configuration;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] String propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private List<Product>? _products;

    public List<Product>? Products
    {
        get => _products;
        set
        {
            _products = value;
            OnPropertyChanged();
        }
    }


    private bool _isDatabaseExist;

    public bool IsDatabaseExist 
    {
        get => _isDatabaseExist;
        set
        {
            _isDatabaseExist = value;
            OnPropertyChanged();
        }
    }

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        Configuration();
    }

    private async void Configuration()
    {
        _configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
        var conStr = _configuration.GetConnectionString("ServerConnectionString");
        _connection = new SqlConnection(conStr);

        var checkDbCommand = @"DECLARE @isDatabaseExist bit = 0

IF EXISTS(SELECT * FROM sys.databases WHERE NAME = 'OnlineStore')
	SET @isDatabaseExist = 1

SELECT @isDatabaseExist";

        IsDatabaseExist = await _connection.ExecuteScalarAsync<bool>(checkDbCommand);

        if(IsDatabaseExist)
        {
            var startIndex = conStr.IndexOf(';') + 1;

            _connection.ConnectionString = conStr.Insert(startIndex, "Database = OnlineStore;");

            IsDatabaseExist = true;
        }
    }

    private async void btnDataBase_Click(object sender, RoutedEventArgs e)
    {
        var databaseCreateCommand = @"IF NOT EXISTS(SELECT * FROM sys.databases WHERE NAME = 'OnlineStore')
CREATE DATABASE OnlineStore";

        var tableCreateCommand = @"IF EXISTS(SELECT * FROM sys.databases WHERE NAME = 'OnlineStore')
BEGIN
USE OnlineStore
IF NOT EXISTS(SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Products')
    BEGIN
    CREATE TABLE Products (
        Id int PRIMARY KEY IDENTITY (1, 1),
        Name nvarchar(40) NOT NULL,
        Country nvarchar(40) NULL,
        Price money NOT NULL,
        Count int NOT NULL DEFAULT(0)
    );
    IF EXISTS(SELECT * FROM sys.databases WHERE NAME = 'Northwind')
    BEGIN
        INSERT INTO OnlineStore.dbo.Products(Name,Price,Count)
        SELECT [ProductName] AS Name
        ,[UnitPrice] AS Price
        ,[UnitsInStock] AS Count
        FROM [Northwind].[dbo].[Products]
    END
    END
END";

        await _connection.ExecuteAsync(databaseCreateCommand);
        await _connection.ExecuteAsync(tableCreateCommand);

        var conStr = _connection.ConnectionString;

        var startIndex = conStr.IndexOf(';') +1;

        _connection.ConnectionString = conStr.Insert(startIndex,"Database = OnlineStore;");

        IsDatabaseExist = true;
    }

    private async void btnRefresh_Click(object sender, RoutedEventArgs e)
    {
        var getDataCommand = "SELECT * FROM Products";

        var collection = await _connection.QueryAsync<Product>(getDataCommand);

        Products= collection.ToList();
    }

    private async void btnSearch_Click(object sender, RoutedEventArgs e)
    {
        if(string.IsNullOrWhiteSpace(SearchTxt.Text)) 
            return;

        var getDataCommand = $"SELECT * FROM OnlineStore.dbo.Products WHERE Name LIKE '%{SearchTxt.Text}%'";

        var collection = await _connection.QueryAsync<Product>(getDataCommand);

        Products = collection.ToList();
    }

    private void btnDelete_Click(object sender, RoutedEventArgs e)
    {
        DapperPlusManager.Entity<Product>().Table("Products");

        var collection = DataList.SelectedItems.Cast<Product>().ToList();

        if (collection.Count == 0 || Products is null)
            return;

        var startIndex = Products.IndexOf(collection[0]);

        foreach (var item in collection)
            Products.Remove(item);

        _connection.BulkDelete(collection);
    }
}
