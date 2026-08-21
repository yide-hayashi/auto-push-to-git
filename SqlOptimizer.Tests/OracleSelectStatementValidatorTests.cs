using Project_auto_push_to_gitlab.SqlOptimizer.Core;
using Xunit;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Tests;

public sealed class OracleSelectStatementValidatorTests
{
    private readonly OracleSelectStatementValidator _validator = new();

    [Theory]
    [InlineData("select 1 from dual", "select 1 from dual")]
    [InlineData(" SELECT sysdate FROM dual; ", "SELECT sysdate FROM dual")]
    [InlineData("select 1 from dual; -- trailing comment", "select 1 from dual")]
    [InlineData(
        "with source_rows as (select 1 as id from dual) select id from source_rows",
        "with source_rows as (select 1 as id from dual) select id from source_rows")]
    [InlineData(
        "/* DROP TABLE audit_log; */ select 1 from dual -- UPDATE audit_log",
        "/* DROP TABLE audit_log; */ select 1 from dual -- UPDATE audit_log")]
    [InlineData(
        "select 'DELETE FROM t; :not_a_bind' as text_value, :real_bind from dual",
        "select 'DELETE FROM t; :not_a_bind' as text_value, :real_bind from dual")]
    [InlineData(
        "select q'[UPDATE t SET value = :ignored;]' as text_value, :actual from dual",
        "select q'[UPDATE t SET value = :ignored;]' as text_value, :actual from dual")]
    [InlineData(
        "select \"UPDATE\" as \"DROP\", :id from dual",
        "select \"UPDATE\" as \"DROP\", :id from dual")]
    public void Validate_AcceptsOnlyOneReadOnlySelect(
        string sql,
        string expectedExecutableSql)
    {
        SqlValidationResult result = _validator.Validate(sql);

        Assert.Equal(expectedExecutableSql, result.ExecutableSql);
    }

    [Fact]
    public void Validate_ExtractsDistinctBindNames_AndIgnoresCommentsAndStrings()
    {
        const string sql = """
            select :id, :ID, :"Mixed Name", ':string_bind'
              from dual
             where 1 = 1 /* :comment_bind */
               and :other_value is not null
            """;

        SqlValidationResult result = _validator.Validate(sql);

        Assert.Equal(
            ["ID", "MIXED NAME", "OTHER_VALUE"],
            result.BindNames);
    }

    [Theory]
    [InlineData("insert into t(id) values (1)")]
    [InlineData("update t set value = 1")]
    [InlineData("delete from t")]
    [InlineData("merge into t using dual on (1 = 1) when matched then update set value = 1")]
    [InlineData("create table t(id number)")]
    [InlineData("alter table t add value number")]
    [InlineData("drop table t")]
    [InlineData("truncate table t")]
    [InlineData("begin null; end;")]
    [InlineData("declare value number; begin value := 1; end;")]
    [InlineData("call dangerous_procedure()")]
    [InlineData("explain plan for select 1 from dual")]
    [InlineData("with changed as (delete from t returning id) select id from changed")]
    public void Validate_RejectsDmlDdlAndPlSql(string sql)
    {
        Assert.Throws<SqlValidationException>(() => _validator.Validate(sql));
    }

    [Theory]
    [InlineData("select 1 from dual; select 2 from dual")]
    [InlineData("select 1 from dual; drop table t")]
    [InlineData("select 1 from dual;;")]
    public void Validate_RejectsMultipleStatements(string sql)
    {
        SqlValidationException exception =
            Assert.Throws<SqlValidationException>(() => _validator.Validate(sql));

        Assert.Contains("Multiple", exception.Message);
    }

    [Theory]
    [InlineData("select * from t for update")]
    [InlineData("select * from t for /* lock rows */ update")]
    [InlineData("select * from t\nfor\nupdate")]
    public void Validate_RejectsForUpdate(string sql)
    {
        SqlValidationException exception =
            Assert.Throws<SqlValidationException>(() => _validator.Validate(sql));

        Assert.Contains("FOR UPDATE", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   -- comment only")]
    [InlineData("select 'unterminated from dual")]
    [InlineData("select q'[unterminated' from dual")]
    [InlineData("select 1 from dual /* unterminated")]
    [InlineData("select (1 from dual")]
    [InlineData("select 1) from dual")]
    public void Validate_RejectsMalformedOrMissingSql(string sql)
    {
        Assert.Throws<SqlValidationException>(() => _validator.Validate(sql));
    }

    [Fact]
    public void EnsureBindingsMatch_AcceptsCaseInsensitiveExactSchema()
    {
        SqlValidationResult validation =
            _validator.Validate("select :patient_id, :visit_date from dual");
        SqlBindParameter[] parameters =
        [
            new("PATIENT_ID", OracleBindDataType.Int64, 123L),
            new("visit_date", OracleBindDataType.Date, new DateTime(2026, 7, 24))
        ];

        _validator.EnsureBindingsMatch(validation, parameters);
    }

    [Fact]
    public void EnsureBindingsMatch_RejectsMissingUnexpectedAndDuplicateBindings()
    {
        SqlValidationResult validation =
            _validator.Validate("select :patient_id, :visit_date from dual");

        Assert.Throws<SqlValidationException>(
            () => _validator.EnsureBindingsMatch(
                validation,
                [new SqlBindParameter("patient_id", OracleBindDataType.Int64, 1L)]));

        Assert.Throws<SqlValidationException>(
            () => _validator.EnsureBindingsMatch(
                validation,
                [
                    new SqlBindParameter("patient_id", OracleBindDataType.Int64, 1L),
                    new SqlBindParameter("visit_date", OracleBindDataType.Date, DateTime.Today),
                    new SqlBindParameter("extra", OracleBindDataType.Varchar2, "x")
                ]));

        Assert.Throws<SqlValidationException>(
            () => _validator.EnsureBindingsMatch(
                validation,
                [
                    new SqlBindParameter("patient_id", OracleBindDataType.Int64, 1L),
                    new SqlBindParameter("PATIENT_ID", OracleBindDataType.Int64, 2L),
                    new SqlBindParameter("visit_date", OracleBindDataType.Date, DateTime.Today)
                ]));
    }
}
