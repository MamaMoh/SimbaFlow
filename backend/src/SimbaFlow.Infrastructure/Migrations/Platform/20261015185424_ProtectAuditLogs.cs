using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimbaFlow.Infrastructure.Migrations.Platform
{
    /// <summary>
    /// Makes both audit logs append-only in the database itself. The application never edits or
    /// deletes a row, but nothing stopped a stray query, a script or a compromised account from
    /// doing it. Now any UPDATE, DELETE or TRUNCATE on them is refused.
    ///
    /// A deliberate retention clean-up by whoever runs the database can still be done inside a
    /// transaction that first runs <c>SET LOCAL simbaflow.audit_maintenance = 'on'</c>.
    /// </summary>
    public partial class ProtectAuditLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION public.audit_log_append_only() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF current_setting('simbaflow.audit_maintenance', true) = 'on' THEN
                        RETURN COALESCE(NEW, OLD);
                    END IF;
                    RAISE EXCEPTION 'The audit log is append-only: % on % is not allowed.', TG_OP, TG_TABLE_NAME
                        USING ERRCODE = 'insufficient_privilege';
                END
                $$;
                """);

            foreach (var table in new[] { "AuditLogs", "ReadAuditLogs" })
            {
                migrationBuilder.Sql($"""
                    CREATE TRIGGER "{table}_no_change"
                        BEFORE UPDATE OR DELETE ON public."{table}"
                        FOR EACH ROW EXECUTE FUNCTION public.audit_log_append_only();
                    CREATE TRIGGER "{table}_no_truncate"
                        BEFORE TRUNCATE ON public."{table}"
                        FOR EACH STATEMENT EXECUTE FUNCTION public.audit_log_append_only();
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in new[] { "AuditLogs", "ReadAuditLogs" })
            {
                migrationBuilder.Sql($"""
                    DROP TRIGGER IF EXISTS "{table}_no_change" ON public."{table}";
                    DROP TRIGGER IF EXISTS "{table}_no_truncate" ON public."{table}";
                    """);
            }

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS public.audit_log_append_only();");
        }
    }
}
