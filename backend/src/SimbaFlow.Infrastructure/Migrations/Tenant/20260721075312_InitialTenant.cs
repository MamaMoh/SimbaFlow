using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimbaFlow.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class InitialTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "candidates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_name = table.Column<string>(type: "text", nullable: false),
                    last_name = table.Column<string>(type: "text", nullable: false),
                    middle_name = table.Column<string>(type: "text", nullable: true),
                    passport_number = table.Column<string>(type: "text", nullable: false),
                    labour_id = table.Column<string>(type: "text", nullable: true),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: false),
                    gender = table.Column<int>(type: "integer", nullable: false),
                    nationality = table.Column<string>(type: "text", nullable: true),
                    phone_number = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true),
                    address = table.Column<string>(type: "text", nullable: true),
                    city = table.Column<string>(type: "text", nullable: true),
                    country = table.Column<string>(type: "text", nullable: true),
                    country_of_travel = table.Column<string>(type: "text", nullable: true),
                    office_name = table.Column<string>(type: "text", nullable: true),
                    contract_date = table.Column<DateOnly>(type: "date", nullable: true),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    photo_path = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    current_stage_id = table.Column<Guid>(type: "uuid", nullable: true),
                    current_stage_name = table.Column<string>(type: "text", nullable: true),
                    current_status_values = table.Column<string>(type: "text", nullable: true),
                    visible_in_stages = table.Column<string>(type: "text", nullable: false),
                    registered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    registered_by = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_candidates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenant_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_system_role = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "workflow_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workflow_definitions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "workflow_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence_number = table.Column<long>(type: "bigint", nullable: false),
                    event_type = table.Column<int>(type: "integer", nullable: false),
                    from_stage_id = table.Column<Guid>(type: "uuid", nullable: true),
                    from_stage_name = table.Column<string>(type: "text", nullable: true),
                    to_stage_id = table.Column<Guid>(type: "uuid", nullable: true),
                    to_stage_name = table.Column<string>(type: "text", nullable: true),
                    data = table.Column<string>(type: "text", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_name = table.Column<string>(type: "text", nullable: false),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workflow_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "workflow_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence_number = table.Column<long>(type: "bigint", nullable: false),
                    stage_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stage_name = table.Column<string>(type: "text", nullable: false),
                    status_values = table.Column<string>(type: "text", nullable: false),
                    visible_in_stages = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workflow_snapshots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "candidate_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "text", nullable: false),
                    original_file_name = table.Column<string>(type: "text", nullable: false),
                    content_type = table.Column<string>(type: "text", nullable: false),
                    file_path = table.Column<string>(type: "text", nullable: false),
                    thumbnail_path = table.Column<string>(type: "text", nullable: true),
                    document_type = table.Column<int>(type: "integer", nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    uploaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    uploaded_by = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_candidate_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_candidate_documents_candidates_candidate_id",
                        column: x => x.candidate_id,
                        principalTable: "candidates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_role_permissions",
                columns: table => new
                {
                    tenant_role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_code = table.Column<string>(type: "text", nullable: false),
                    granted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    granted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_role_permissions", x => new { x.tenant_role_id, x.permission_code });
                    table.ForeignKey(
                        name: "fk_tenant_role_permissions_tenant_roles_tenant_role_id",
                        column: x => x.tenant_role_id,
                        principalTable: "tenant_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_user_roles",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    assigned_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_user_roles", x => new { x.user_id, x.tenant_role_id });
                    table.ForeignKey(
                        name: "fk_tenant_user_roles_tenant_roles_tenant_role_id",
                        column: x => x.tenant_role_id,
                        principalTable: "tenant_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workflow_stages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workflow_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    stage_type = table.Column<int>(type: "integer", nullable: false),
                    is_initial_stage = table.Column<bool>(type: "boolean", nullable: false),
                    is_final_stage = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workflow_stages", x => x.id);
                    table.ForeignKey(
                        name: "fk_workflow_stages_workflow_definitions_workflow_definition_id",
                        column: x => x.workflow_definition_id,
                        principalTable: "workflow_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mirror_view_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workflow_stage_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_stage_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conditions = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mirror_view_rules", x => x.id);
                    table.ForeignKey(
                        name: "fk_mirror_view_rules_workflow_stages_target_stage_id",
                        column: x => x.target_stage_id,
                        principalTable: "workflow_stages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_mirror_view_rules_workflow_stages_workflow_stage_id",
                        column: x => x.workflow_stage_id,
                        principalTable: "workflow_stages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "parallel_track_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workflow_stage_id = table.Column<Guid>(type: "uuid", nullable: false),
                    track_name = table.Column<string>(type: "text", nullable: false),
                    completion_status = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_parallel_track_definitions", x => x.id);
                    table.ForeignKey(
                        name: "fk_parallel_track_definitions_workflow_stages_workflow_stage_id",
                        column: x => x.workflow_stage_id,
                        principalTable: "workflow_stages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workflow_stage_statuses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workflow_stage_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_terminal = table.Column<bool>(type: "boolean", nullable: false),
                    track_name = table.Column<string>(type: "text", nullable: true),
                    color = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workflow_stage_statuses", x => x.id);
                    table.ForeignKey(
                        name: "fk_workflow_stage_statuses_workflow_stages_workflow_stage_id",
                        column: x => x.workflow_stage_id,
                        principalTable: "workflow_stages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workflow_transition_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workflow_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_stage_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_stage_id = table.Column<Guid>(type: "uuid", nullable: false),
                    button_label = table.Column<string>(type: "text", nullable: false),
                    button_icon = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    conditions = table.Column<string>(type: "text", nullable: false),
                    required_fields = table.Column<string>(type: "text", nullable: false),
                    allowed_roles = table.Column<string>(type: "text", nullable: false),
                    remove_from_source = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workflow_transition_rules", x => x.id);
                    table.ForeignKey(
                        name: "fk_workflow_transition_rules_workflow_definitions_workflow_def",
                        column: x => x.workflow_definition_id,
                        principalTable: "workflow_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_workflow_transition_rules_workflow_stages_source_stage_id",
                        column: x => x.source_stage_id,
                        principalTable: "workflow_stages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_workflow_transition_rules_workflow_stages_target_stage_id",
                        column: x => x.target_stage_id,
                        principalTable: "workflow_stages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stage_mandatory_fields",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workflow_stage_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field_name = table.Column<string>(type: "text", nullable: false),
                    transition_rule_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stage_mandatory_fields", x => x.id);
                    table.ForeignKey(
                        name: "fk_stage_mandatory_fields_workflow_stages_workflow_stage_id",
                        column: x => x.workflow_stage_id,
                        principalTable: "workflow_stages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_stage_mandatory_fields_workflow_transition_rules_transition",
                        column: x => x.transition_rule_id,
                        principalTable: "workflow_transition_rules",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "ix_candidate_documents_candidate_id",
                table: "candidate_documents",
                column: "candidate_id");

            migrationBuilder.CreateIndex(
                name: "ix_candidates_current_stage_id",
                table: "candidates",
                column: "current_stage_id");

            migrationBuilder.CreateIndex(
                name: "ix_candidates_labour_id",
                table: "candidates",
                column: "labour_id",
                unique: true,
                filter: "labour_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_candidates_office_id",
                table: "candidates",
                column: "office_id");

            migrationBuilder.CreateIndex(
                name: "ix_candidates_passport_number",
                table: "candidates",
                column: "passport_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mirror_view_rules_target_stage_id",
                table: "mirror_view_rules",
                column: "target_stage_id");

            migrationBuilder.CreateIndex(
                name: "ix_mirror_view_rules_workflow_stage_id",
                table: "mirror_view_rules",
                column: "workflow_stage_id");

            migrationBuilder.CreateIndex(
                name: "ix_parallel_track_definitions_workflow_stage_id",
                table: "parallel_track_definitions",
                column: "workflow_stage_id");

            migrationBuilder.CreateIndex(
                name: "ix_stage_mandatory_fields_transition_rule_id",
                table: "stage_mandatory_fields",
                column: "transition_rule_id");

            migrationBuilder.CreateIndex(
                name: "ix_stage_mandatory_fields_workflow_stage_id",
                table: "stage_mandatory_fields",
                column: "workflow_stage_id");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_user_roles_tenant_role_id",
                table: "tenant_user_roles",
                column: "tenant_role_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_events_candidate_id_sequence_number",
                table: "workflow_events",
                columns: new[] { "candidate_id", "sequence_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_workflow_events_timestamp",
                table: "workflow_events",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_snapshots_candidate_id_sequence_number",
                table: "workflow_snapshots",
                columns: new[] { "candidate_id", "sequence_number" });

            migrationBuilder.CreateIndex(
                name: "ix_workflow_stage_statuses_workflow_stage_id",
                table: "workflow_stage_statuses",
                column: "workflow_stage_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_stages_workflow_definition_id",
                table: "workflow_stages",
                column: "workflow_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_transition_rules_source_stage_id",
                table: "workflow_transition_rules",
                column: "source_stage_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_transition_rules_target_stage_id",
                table: "workflow_transition_rules",
                column: "target_stage_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_transition_rules_workflow_definition_id",
                table: "workflow_transition_rules",
                column: "workflow_definition_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "candidate_documents");

            migrationBuilder.DropTable(
                name: "mirror_view_rules");

            migrationBuilder.DropTable(
                name: "parallel_track_definitions");

            migrationBuilder.DropTable(
                name: "stage_mandatory_fields");

            migrationBuilder.DropTable(
                name: "tenant_role_permissions");

            migrationBuilder.DropTable(
                name: "tenant_user_roles");

            migrationBuilder.DropTable(
                name: "workflow_events");

            migrationBuilder.DropTable(
                name: "workflow_snapshots");

            migrationBuilder.DropTable(
                name: "workflow_stage_statuses");

            migrationBuilder.DropTable(
                name: "candidates");

            migrationBuilder.DropTable(
                name: "workflow_transition_rules");

            migrationBuilder.DropTable(
                name: "tenant_roles");

            migrationBuilder.DropTable(
                name: "workflow_stages");

            migrationBuilder.DropTable(
                name: "workflow_definitions");
        }
    }
}
