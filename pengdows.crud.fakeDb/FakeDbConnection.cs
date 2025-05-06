#region

using pengdows.crud.enums;

#endregion

namespace pengdows.crud.FakeDb;

using System;
using System.Data;
using System.Data.Common;
using System.IO;

public class FakeDbConnection : DbConnection, IDbConnection
{
    private string _connectionString = string.Empty;
    private ConnectionState _state = ConnectionState.Closed;
    private SupportedDatabase? _emulatedProduct;
    private DataTable? _schemaTable;

    public override string ConnectionString
    {
        get => _connectionString;
        set => _connectionString = value;
    }

    public int ConnectionTimeout { get; }
    public override string Database => _emulatedProduct?.ToString() ?? string.Empty;
    public override string DataSource => "FakeSource";
    public override string ServerVersion => "1.0";

    public override ConnectionState State
    {
        get => _state;
    }

    public SupportedDatabase EmulatedProduct
    {
        get
        {
            _emulatedProduct ??= SupportedDatabase.Unknown; 
            return _emulatedProduct.Value;
        }
        set
        {
            if (_emulatedProduct == null || _emulatedProduct == SupportedDatabase.Unknown)
            {
                _emulatedProduct = value;
            }
        }
    }

    private SupportedDatabase ParseEmulatedProduct(string connStr)
    {
        if (EmulatedProduct == SupportedDatabase.Unknown)
        {
            var builder = new DbConnectionStringBuilder { ConnectionString = connStr };
            if (!builder.TryGetValue("EmulatedProduct", out var raw))
            {
                EmulatedProduct = SupportedDatabase.Unknown;
            }
            else
            {
                EmulatedProduct = Enum.TryParse<SupportedDatabase>(raw.ToString(), ignoreCase: true, out var result)
                    ? result
                    : throw new ArgumentException($"Invalid EmulatedProduct: {raw}");
            }
        }

        return EmulatedProduct;
    }


    public override void Open()
    {
        ParseEmulatedProduct(ConnectionString);
        _state = ConnectionState.Open;
    }

    public override void Close()
    {
        _state = ConnectionState.Closed;
    }

    public override void ChangeDatabase(string databaseName)
    {
        throw new NotSupportedException();
    }

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
    {
        return new FakeDbTransaction(this, isolationLevel);
    }

    protected override DbCommand CreateDbCommand()
    {
        return new FakeDbCommand(this);
    }

    public override DataTable GetSchema()
    {
        if (_schemaTable != null)
        {
            return _schemaTable;
        }

        if (_emulatedProduct is null or SupportedDatabase.Unknown)
        {
            throw new InvalidOperationException("EmulatedProduct must be configured via connection string.");
        }

        var resourceName = $"pengdows.crud.fakeDb.xml.{_emulatedProduct}.schema.xml";

        using var stream = typeof(FakeDbConnection).Assembly
                               .GetManifestResourceStream(resourceName)
                           ?? throw new FileNotFoundException($"Embedded schema not found: {resourceName}");

        var table = new DataTable();
        table.ReadXml(stream);
        _schemaTable = table;
        return _schemaTable;
    }

    public override DataTable GetSchema(string meta)
    {
        if (_schemaTable != null) return _schemaTable;

        if (_emulatedProduct is null or SupportedDatabase.Unknown)
        {
            throw new InvalidOperationException("EmulatedProduct must be configured via connection string.");
        }

        var resourceName = $"pengdows.crud.fakeDb.xml.{_emulatedProduct}.schema.xml";

        using var stream = typeof(FakeDbConnection).Assembly
                               .GetManifestResourceStream(resourceName)
                           ?? throw new FileNotFoundException($"Embedded schema not found: {resourceName}");

        var table = new DataTable();
        table.ReadXml(stream);
        _schemaTable = table;
        return _schemaTable;
    }
}