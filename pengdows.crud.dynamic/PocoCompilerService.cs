using System.Data;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using pengdows.crud.dynamic;
using pengdows.crud.enums;

namespace WebApplication1.Generator;

public static class PocoCompilerService
{
    private const string OutputDir = "poco_cache";

    public static Assembly GetOrCreateAssembly(List<TableDef> tables)
    {
        var hash = GetSchemaHash(tables);
        var dllPath = Path.Combine(OutputDir, $"poco_{hash}.dll");

        if (File.Exists(dllPath))
        {
            return Assembly.LoadFrom(dllPath);
        }

        var code = GenerateCode(tables);
        var assembly = Compile(code, dllPath);
        return assembly;
    }

    private static string GetSchemaHash(List<TableDef> tables)
    {
        using var sha256 = SHA256.Create();
        var raw = string.Join("\n", tables.OrderBy(t => t.Name).Select(t =>
            $"{t.Name}:{string.Join(",", t.Columns.OrderBy(c => c.Name).Select(c => $"{c.Name}:{c.Type}"))}"));
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash);
    }

    private static string GenerateCode(List<TableDef> tables)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using pengdows.crud.attributes;");
        sb.AppendLine("namespace DynamicEntities {");

        foreach (var table in tables)
        {
            sb.Append($"  [Table(\"{table.Name}\"");
            if (!string.IsNullOrEmpty(table.Schema))
            {
                sb.Append(", \"{table.Schema}\"");
            }

            sb.AppendLine($")]\n  public class {table.Name} {{");

            foreach (var col in table.Columns)
            {
                WriteColumnAttributes(sb, col);
                
                sb.AppendLine($"    public {col.Type} {col.Name} {{ get; set; }}");
            }

            sb.AppendLine("  }");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void WriteColumnAttributes(StringBuilder sb, ColumnDef col)
    {
        
    }

    private static Assembly Compile(string sourceCode, string dllPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dllPath)!);

        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .ToList();

        var compilation = CSharpCompilation.Create(
            Path.GetFileNameWithoutExtension(dllPath),
            new[] { syntaxTree },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        using var fs = new FileStream(dllPath, FileMode.Create);
        var result = compilation.Emit(fs);
        if (!result.Success)
        {
            var errors = string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            throw new Exception($"Compilation failed:\n{errors}");
        }

        return Assembly.LoadFrom(dllPath);
    }

    public static DbType? MapToDbType(SupportedDatabase db, string typeName, int? precision = null, int? scale = null)
    {
        var type = typeName.ToLowerInvariant();

        return db switch
        {
            SupportedDatabase.SqlServer => MapSqlServer(type, precision, scale),
            SupportedDatabase.PostgreSql or SupportedDatabase.CockroachDb => MapPostgres(type, precision, scale),
            SupportedDatabase.MySql or SupportedDatabase.MariaDb => MapMySql(type, precision, scale),
            SupportedDatabase.Oracle => MapOracle(type, precision, scale),
            SupportedDatabase.Sqlite => MapSqlite(type, precision, scale),
            SupportedDatabase.Firebird => MapFirebird(type, precision, scale),
            SupportedDatabase.Sybase => MapSybase(type, precision, scale),
            _ => null
        };
    }

    private static DbType? MapSqlServer(string type, int? precision, int? scale) => type switch
    {
        "int" => DbType.Int32,
        "bigint" => DbType.Int64,
        "smallint" => DbType.Int16,
        "tinyint" => DbType.Byte,
        "bit" => DbType.Boolean,
        "varchar" or "nvarchar" or "text" or "ntext" => DbType.String,
        "char" or "nchar" => DbType.StringFixedLength,
        "datetime" or "smalldatetime" => DbType.DateTime,
        "decimal" or "numeric" => MapDecimal(precision, scale),
        _ => null
    };

    private static DbType? MapPostgres(string type, int? precision, int? scale) => type switch
    {
        "int4" or "integer" => DbType.Int32,
        "int8" or "bigint" => DbType.Int64,
        "int2" or "smallint" => DbType.Int16,
        "bool" or "boolean" => DbType.Boolean,
        "text" or "varchar" => DbType.String,
        "uuid" => DbType.Guid,
        "numeric" or "decimal" => MapDecimal(precision, scale),
        _ => null
    };

    private static DbType? MapMySql(string type, int? precision, int? scale) => type switch
    {
        "int" or "integer" => DbType.Int32,
        "bigint" => DbType.Int64,
        "smallint" => DbType.Int16,
        "tinyint" => DbType.Byte,
        "bit" => DbType.Boolean,
        "varchar" or "text" => DbType.String,
        "decimal" or "numeric" => MapDecimal(precision, scale),
        _ => null
    };

    private static DbType? MapOracle(string type, int? precision, int? scale) => type switch
    {
        "number" => MapDecimal(precision, scale),
        "varchar2" or "nvarchar2" => DbType.String,
        "char" or "nchar" => DbType.StringFixedLength,
        "date" => DbType.DateTime,
        _ => null
    };

    private static DbType? MapSqlite(string type, int? precision, int? scale) => type switch
    {
        "integer" => DbType.Int64,
        "text" => DbType.String,
        "real" => DbType.Double,
        "numeric" => MapDecimal(precision, scale),
        _ => null
    };

    private static DbType? MapFirebird(string type, int? precision, int? scale) => type switch
    {
        "smallint" => DbType.Int16,
        "integer" => DbType.Int32,
        "bigint" => DbType.Int64,
        "varchar" or "char" => DbType.String,
        "decimal" or "numeric" => MapDecimal(precision, scale),
        _ => null
    };

    private static DbType? MapSybase(string type, int? precision, int? scale) => type switch
    {
        "int" => DbType.Int32,
        "bigint" => DbType.Int64,
        "smallint" => DbType.Int16,
        "tinyint" => DbType.Byte,
        "bit" => DbType.Boolean,
        "varchar" or "text" => DbType.String,
        "decimal" or "numeric" => MapDecimal(precision, scale),
        _ => null
    };

    private static DbType MapDecimal(int? precision, int? scale)
    {
        if (scale == 2 && precision <= 19)
            return DbType.Currency;
        if ((precision ?? 0) > 29 || (scale ?? 0) > 28)
            return DbType.VarNumeric;
        return DbType.Decimal;
    }
}