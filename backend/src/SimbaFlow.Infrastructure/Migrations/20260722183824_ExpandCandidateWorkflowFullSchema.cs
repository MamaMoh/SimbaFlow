using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimbaFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExpandCandidateWorkflowFullSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CandidateDocuments_Candidates_CandidateId",
                table: "CandidateDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_MirrorViewRule_WorkflowStages_TargetStageId",
                table: "MirrorViewRule");

            migrationBuilder.DropForeignKey(
                name: "FK_MirrorViewRule_WorkflowStages_WorkflowStageId",
                table: "MirrorViewRule");

            migrationBuilder.DropForeignKey(
                name: "FK_ParallelTrackDefinition_WorkflowStages_WorkflowStageId",
                table: "ParallelTrackDefinition");

            migrationBuilder.DropForeignKey(
                name: "FK_StageMandatoryField_WorkflowStages_WorkflowStageId",
                table: "StageMandatoryField");

            migrationBuilder.DropForeignKey(
                name: "FK_StageMandatoryField_WorkflowTransitionRule_TransitionRuleId",
                table: "StageMandatoryField");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowStages_WorkflowDefinitions_WorkflowDefinitionId",
                table: "WorkflowStages");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowStageStatus_WorkflowStages_WorkflowStageId",
                table: "WorkflowStageStatus");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowTransitionRule_WorkflowDefinitions_WorkflowDefiniti~",
                table: "WorkflowTransitionRule");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowTransitionRule_WorkflowStages_SourceStageId",
                table: "WorkflowTransitionRule");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowTransitionRule_WorkflowStages_TargetStageId",
                table: "WorkflowTransitionRule");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Candidates",
                table: "Candidates");

            migrationBuilder.DropPrimaryKey(
                name: "PK_WorkflowTransitionRule",
                table: "WorkflowTransitionRule");

            migrationBuilder.DropPrimaryKey(
                name: "PK_WorkflowStageStatus",
                table: "WorkflowStageStatus");

            migrationBuilder.DropPrimaryKey(
                name: "PK_WorkflowStages",
                table: "WorkflowStages");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowStages_WorkflowDefinitionId",
                table: "WorkflowStages");

            migrationBuilder.DropPrimaryKey(
                name: "PK_WorkflowSnapshots",
                table: "WorkflowSnapshots");

            migrationBuilder.DropPrimaryKey(
                name: "PK_WorkflowEvents",
                table: "WorkflowEvents");

            migrationBuilder.DropPrimaryKey(
                name: "PK_WorkflowDefinitions",
                table: "WorkflowDefinitions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_StageMandatoryField",
                table: "StageMandatoryField");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ParallelTrackDefinition",
                table: "ParallelTrackDefinition");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MirrorViewRule",
                table: "MirrorViewRule");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CandidateDocuments",
                table: "CandidateDocuments");

            migrationBuilder.RenameTable(
                name: "Candidates",
                newName: "candidates");

            migrationBuilder.RenameTable(
                name: "WorkflowTransitionRule",
                newName: "WorkflowTransitionRules");

            migrationBuilder.RenameTable(
                name: "WorkflowStageStatus",
                newName: "WorkflowStageStatuses");

            migrationBuilder.RenameTable(
                name: "WorkflowStages",
                newName: "workflow_stages");

            migrationBuilder.RenameTable(
                name: "WorkflowSnapshots",
                newName: "workflow_snapshots");

            migrationBuilder.RenameTable(
                name: "WorkflowEvents",
                newName: "workflow_events");

            migrationBuilder.RenameTable(
                name: "WorkflowDefinitions",
                newName: "workflow_definitions");

            migrationBuilder.RenameTable(
                name: "StageMandatoryField",
                newName: "StageMandatoryFields");

            migrationBuilder.RenameTable(
                name: "ParallelTrackDefinition",
                newName: "ParallelTrackDefinitions");

            migrationBuilder.RenameTable(
                name: "MirrorViewRule",
                newName: "MirrorViewRules");

            migrationBuilder.RenameTable(
                name: "CandidateDocuments",
                newName: "candidate_documents");

            migrationBuilder.RenameIndex(
                name: "IX_WorkflowTransitionRule_WorkflowDefinitionId",
                table: "WorkflowTransitionRules",
                newName: "IX_WorkflowTransitionRules_WorkflowDefinitionId");

            migrationBuilder.RenameIndex(
                name: "IX_WorkflowTransitionRule_TargetStageId",
                table: "WorkflowTransitionRules",
                newName: "IX_WorkflowTransitionRules_TargetStageId");

            migrationBuilder.RenameIndex(
                name: "IX_WorkflowTransitionRule_SourceStageId",
                table: "WorkflowTransitionRules",
                newName: "IX_WorkflowTransitionRules_SourceStageId");

            migrationBuilder.RenameIndex(
                name: "IX_WorkflowStageStatus_WorkflowStageId",
                table: "WorkflowStageStatuses",
                newName: "IX_WorkflowStageStatuses_WorkflowStageId");

            migrationBuilder.RenameIndex(
                name: "IX_StageMandatoryField_WorkflowStageId",
                table: "StageMandatoryFields",
                newName: "IX_StageMandatoryFields_WorkflowStageId");

            migrationBuilder.RenameIndex(
                name: "IX_StageMandatoryField_TransitionRuleId",
                table: "StageMandatoryFields",
                newName: "IX_StageMandatoryFields_TransitionRuleId");

            migrationBuilder.RenameIndex(
                name: "IX_ParallelTrackDefinition_WorkflowStageId",
                table: "ParallelTrackDefinitions",
                newName: "IX_ParallelTrackDefinitions_WorkflowStageId");

            migrationBuilder.RenameIndex(
                name: "IX_MirrorViewRule_WorkflowStageId",
                table: "MirrorViewRules",
                newName: "IX_MirrorViewRules_WorkflowStageId");

            migrationBuilder.RenameIndex(
                name: "IX_MirrorViewRule_TargetStageId",
                table: "MirrorViewRules",
                newName: "IX_MirrorViewRules_TargetStageId");

            migrationBuilder.RenameIndex(
                name: "IX_CandidateDocuments_CandidateId",
                table: "candidate_documents",
                newName: "IX_candidate_documents_CandidateId");

            migrationBuilder.AlterColumn<string>(
                name: "PassportNumber",
                table: "candidates",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "LastName",
                table: "candidates",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "FirstName",
                table: "candidates",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "ApplicationNo",
                table: "candidates",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BiometricId",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CurrentStageEnteredAt",
                table: "candidates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileNumber",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FlightDate",
                table: "candidates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HouseNo",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOverdue",
                table: "candidates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastActionAt",
                table: "candidates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastActionLabel",
                table: "candidates",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaritalStatus",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NationalId",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Occupation",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PassportExpiryDate",
                table: "candidates",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PassportIssueDate",
                table: "candidates",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PassportType",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone2",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlaceOfBirth",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlaceOfIssue",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Qualification",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Region",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Religion",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Subcity",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Woreda",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowDefinitionId1",
                table: "WorkflowTransitionRules",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "workflow_stages",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<int>(
                name: "CriticalDurationHours",
                table: "workflow_stages",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExpectedDurationHours",
                table: "workflow_stages",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WarningDurationHours",
                table: "workflow_stages",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "workflow_definitions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "OriginalFileName",
                table: "candidate_documents",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                table: "candidate_documents",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddPrimaryKey(
                name: "PK_candidates",
                table: "candidates",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_WorkflowTransitionRules",
                table: "WorkflowTransitionRules",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_WorkflowStageStatuses",
                table: "WorkflowStageStatuses",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_workflow_stages",
                table: "workflow_stages",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_workflow_snapshots",
                table: "workflow_snapshots",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_workflow_events",
                table: "workflow_events",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_workflow_definitions",
                table: "workflow_definitions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_StageMandatoryFields",
                table: "StageMandatoryFields",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ParallelTrackDefinitions",
                table: "ParallelTrackDefinitions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_MirrorViewRules",
                table: "MirrorViewRules",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_candidate_documents",
                table: "candidate_documents",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "candidate_commissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    SentByStaffId = table.Column<Guid>(type: "uuid", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_commissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_candidate_commissions_candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "candidate_complaints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    ComplaintText = table.Column<string>(type: "text", nullable: false),
                    FiledByStaffId = table.Column<Guid>(type: "uuid", nullable: true),
                    FiledByUserName = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_complaints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_candidate_complaints_candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "candidate_placements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    CountryOfTravel = table.Column<string>(type: "text", nullable: true),
                    WorksIn = table.Column<string>(type: "text", nullable: true),
                    PartnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    VisaNumber = table.Column<string>(type: "text", nullable: true),
                    VisaType = table.Column<string>(type: "text", nullable: true),
                    StickerVisaNumber = table.Column<string>(type: "text", nullable: true),
                    SponsorId = table.Column<string>(type: "text", nullable: true),
                    SponsorName = table.Column<string>(type: "text", nullable: true),
                    SponsorNameArabic = table.Column<string>(type: "text", nullable: true),
                    SponsorPhone = table.Column<string>(type: "text", nullable: true),
                    SponsorAddress = table.Column<string>(type: "text", nullable: true),
                    SponsorEmail = table.Column<string>(type: "text", nullable: true),
                    Agent = table.Column<string>(type: "text", nullable: true),
                    NationalId = table.Column<string>(type: "text", nullable: true),
                    ContractNumber = table.Column<string>(type: "text", nullable: true),
                    WakalaNumber = table.Column<string>(type: "text", nullable: true),
                    SignedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ContractDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CocCenter = table.Column<string>(type: "text", nullable: true),
                    CertifiedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CertificateNumber = table.Column<string>(type: "text", nullable: true),
                    TrainingType = table.Column<string>(type: "text", nullable: true),
                    Salary = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "text", nullable: true),
                    Remarks = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_placements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_candidate_placements_candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "candidate_relatives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    RelativeName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RelativePhone = table.Column<string>(type: "text", nullable: true),
                    RelativeKinship = table.Column<string>(type: "text", nullable: true),
                    Gender = table.Column<int>(type: "integer", nullable: true),
                    BirthDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Region = table.Column<string>(type: "text", nullable: true),
                    City = table.Column<string>(type: "text", nullable: true),
                    Subcity = table.Column<string>(type: "text", nullable: true),
                    Woreda = table.Column<string>(type: "text", nullable: true),
                    HouseNo = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_relatives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_candidate_relatives_candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "candidate_returned",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReturnReason = table.Column<string>(type: "text", nullable: true),
                    ReturnDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ReturnTicketInfo = table.Column<string>(type: "text", nullable: true),
                    CreatedByStaffId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_returned", x => x.Id);
                    table.ForeignKey(
                        name: "FK_candidate_returned_candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "candidate_skills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnglishLevel = table.Column<string>(type: "text", nullable: true),
                    ArabicLevel = table.Column<string>(type: "text", nullable: true),
                    ExperienceAbroad = table.Column<string>(type: "text", nullable: true),
                    ChildrenCount = table.Column<short>(type: "smallint", nullable: true),
                    Height = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    Weight = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    CookingNotes = table.Column<string>(type: "text", nullable: true),
                    CanIron = table.Column<bool>(type: "boolean", nullable: false),
                    CanSew = table.Column<bool>(type: "boolean", nullable: false),
                    CanBabysit = table.Column<bool>(type: "boolean", nullable: false),
                    CanChildcare = table.Column<bool>(type: "boolean", nullable: false),
                    CanArabicCooking = table.Column<bool>(type: "boolean", nullable: false),
                    CanClean = table.Column<bool>(type: "boolean", nullable: false),
                    CanWash = table.Column<bool>(type: "boolean", nullable: false),
                    CanCook = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_skills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_candidate_skills_candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "candidate_stage_stays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EnteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExitedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: true),
                    EnteredByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    EnteredByUserName = table.Column<string>(type: "text", nullable: true),
                    ExitedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExitedByUserName = table.Column<string>(type: "text", nullable: true),
                    ExitReason = table.Column<string>(type: "text", nullable: true),
                    EnterEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExitEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_stage_stays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_candidate_stage_stays_candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "offices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "text", nullable: true),
                    Phone = table.Column<string>(type: "text", nullable: true),
                    Address = table.Column<string>(type: "text", nullable: true),
                    City = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_offices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "partners",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NameArabic = table.Column<string>(type: "text", nullable: true),
                    SponsorId = table.Column<string>(type: "text", nullable: true),
                    Phone = table.Column<string>(type: "text", nullable: true),
                    Email = table.Column<string>(type: "text", nullable: true),
                    Address = table.Column<string>(type: "text", nullable: true),
                    Country = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_partners", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "status_transition_permissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TrackKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ToStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AllowedRoleCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AllowedPermissionCode = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_status_transition_permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "task_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TrackKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_assignments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "candidate_step_stays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageStayId = table.Column<Guid>(type: "uuid", nullable: true),
                    TrackKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StatusValue = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: true),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChangedByUserName = table.Column<string>(type: "text", nullable: true),
                    WorkflowEventId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_step_stays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_candidate_step_stays_candidate_stage_stays_StageStayId",
                        column: x => x.StageStayId,
                        principalTable: "candidate_stage_stays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_candidate_step_stays_candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_candidates_ApplicationNo",
                table: "candidates",
                column: "ApplicationNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_candidates_CurrentStageEnteredAt",
                table: "candidates",
                column: "CurrentStageEnteredAt");

            migrationBuilder.CreateIndex(
                name: "IX_candidates_CurrentStageId",
                table: "candidates",
                column: "CurrentStageId");

            migrationBuilder.CreateIndex(
                name: "IX_candidates_LabourId",
                table: "candidates",
                column: "LabourId",
                unique: true,
                filter: "\"LabourId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_candidates_OfficeId",
                table: "candidates",
                column: "OfficeId");

            migrationBuilder.CreateIndex(
                name: "IX_candidates_PassportNumber",
                table: "candidates",
                column: "PassportNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitionRules_WorkflowDefinitionId1",
                table: "WorkflowTransitionRules",
                column: "WorkflowDefinitionId1");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_stages_WorkflowDefinitionId_SortOrder",
                table: "workflow_stages",
                columns: new[] { "WorkflowDefinitionId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_workflow_snapshots_CandidateId_SequenceNumber",
                table: "workflow_snapshots",
                columns: new[] { "CandidateId", "SequenceNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_workflow_events_CandidateId_SequenceNumber",
                table: "workflow_events",
                columns: new[] { "CandidateId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workflow_events_Timestamp",
                table: "workflow_events",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_candidate_commissions_CandidateId",
                table: "candidate_commissions",
                column: "CandidateId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_candidate_complaints_CandidateId",
                table: "candidate_complaints",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_candidate_placements_CandidateId",
                table: "candidate_placements",
                column: "CandidateId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_candidate_relatives_CandidateId",
                table: "candidate_relatives",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_candidate_returned_CandidateId",
                table: "candidate_returned",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_candidate_skills_CandidateId",
                table: "candidate_skills",
                column: "CandidateId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_candidate_stage_stays_CandidateId_IsCurrent",
                table: "candidate_stage_stays",
                columns: new[] { "CandidateId", "IsCurrent" });

            migrationBuilder.CreateIndex(
                name: "IX_candidate_stage_stays_StageId",
                table: "candidate_stage_stays",
                column: "StageId");

            migrationBuilder.CreateIndex(
                name: "IX_candidate_step_stays_CandidateId_TrackKey_FinishedAt",
                table: "candidate_step_stays",
                columns: new[] { "CandidateId", "TrackKey", "FinishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_candidate_step_stays_StageStayId",
                table: "candidate_step_stays",
                column: "StageStayId");

            migrationBuilder.CreateIndex(
                name: "IX_offices_Code",
                table: "offices",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_status_transition_permissions_TrackKey_ToStatus_AllowedRole~",
                table: "status_transition_permissions",
                columns: new[] { "TrackKey", "ToStatus", "AllowedRoleCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_task_assignments_StaffUserId_TrackKey",
                table: "task_assignments",
                columns: new[] { "StaffUserId", "TrackKey" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_candidate_documents_candidates_CandidateId",
                table: "candidate_documents",
                column: "CandidateId",
                principalTable: "candidates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_candidates_offices_OfficeId",
                table: "candidates",
                column: "OfficeId",
                principalTable: "offices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MirrorViewRules_workflow_stages_TargetStageId",
                table: "MirrorViewRules",
                column: "TargetStageId",
                principalTable: "workflow_stages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MirrorViewRules_workflow_stages_WorkflowStageId",
                table: "MirrorViewRules",
                column: "WorkflowStageId",
                principalTable: "workflow_stages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ParallelTrackDefinitions_workflow_stages_WorkflowStageId",
                table: "ParallelTrackDefinitions",
                column: "WorkflowStageId",
                principalTable: "workflow_stages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StageMandatoryFields_WorkflowTransitionRules_TransitionRule~",
                table: "StageMandatoryFields",
                column: "TransitionRuleId",
                principalTable: "WorkflowTransitionRules",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_StageMandatoryFields_workflow_stages_WorkflowStageId",
                table: "StageMandatoryFields",
                column: "WorkflowStageId",
                principalTable: "workflow_stages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_workflow_stages_workflow_definitions_WorkflowDefinitionId",
                table: "workflow_stages",
                column: "WorkflowDefinitionId",
                principalTable: "workflow_definitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowStageStatuses_workflow_stages_WorkflowStageId",
                table: "WorkflowStageStatuses",
                column: "WorkflowStageId",
                principalTable: "workflow_stages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowTransitionRules_workflow_definitions_WorkflowDefini~",
                table: "WorkflowTransitionRules",
                column: "WorkflowDefinitionId",
                principalTable: "workflow_definitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowTransitionRules_workflow_definitions_WorkflowDefin~1",
                table: "WorkflowTransitionRules",
                column: "WorkflowDefinitionId1",
                principalTable: "workflow_definitions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowTransitionRules_workflow_stages_SourceStageId",
                table: "WorkflowTransitionRules",
                column: "SourceStageId",
                principalTable: "workflow_stages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowTransitionRules_workflow_stages_TargetStageId",
                table: "WorkflowTransitionRules",
                column: "TargetStageId",
                principalTable: "workflow_stages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_candidate_documents_candidates_CandidateId",
                table: "candidate_documents");

            migrationBuilder.DropForeignKey(
                name: "FK_candidates_offices_OfficeId",
                table: "candidates");

            migrationBuilder.DropForeignKey(
                name: "FK_MirrorViewRules_workflow_stages_TargetStageId",
                table: "MirrorViewRules");

            migrationBuilder.DropForeignKey(
                name: "FK_MirrorViewRules_workflow_stages_WorkflowStageId",
                table: "MirrorViewRules");

            migrationBuilder.DropForeignKey(
                name: "FK_ParallelTrackDefinitions_workflow_stages_WorkflowStageId",
                table: "ParallelTrackDefinitions");

            migrationBuilder.DropForeignKey(
                name: "FK_StageMandatoryFields_WorkflowTransitionRules_TransitionRule~",
                table: "StageMandatoryFields");

            migrationBuilder.DropForeignKey(
                name: "FK_StageMandatoryFields_workflow_stages_WorkflowStageId",
                table: "StageMandatoryFields");

            migrationBuilder.DropForeignKey(
                name: "FK_workflow_stages_workflow_definitions_WorkflowDefinitionId",
                table: "workflow_stages");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowStageStatuses_workflow_stages_WorkflowStageId",
                table: "WorkflowStageStatuses");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowTransitionRules_workflow_definitions_WorkflowDefini~",
                table: "WorkflowTransitionRules");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowTransitionRules_workflow_definitions_WorkflowDefin~1",
                table: "WorkflowTransitionRules");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowTransitionRules_workflow_stages_SourceStageId",
                table: "WorkflowTransitionRules");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowTransitionRules_workflow_stages_TargetStageId",
                table: "WorkflowTransitionRules");

            migrationBuilder.DropTable(
                name: "candidate_commissions");

            migrationBuilder.DropTable(
                name: "candidate_complaints");

            migrationBuilder.DropTable(
                name: "candidate_placements");

            migrationBuilder.DropTable(
                name: "candidate_relatives");

            migrationBuilder.DropTable(
                name: "candidate_returned");

            migrationBuilder.DropTable(
                name: "candidate_skills");

            migrationBuilder.DropTable(
                name: "candidate_step_stays");

            migrationBuilder.DropTable(
                name: "offices");

            migrationBuilder.DropTable(
                name: "partners");

            migrationBuilder.DropTable(
                name: "status_transition_permissions");

            migrationBuilder.DropTable(
                name: "task_assignments");

            migrationBuilder.DropTable(
                name: "candidate_stage_stays");

            migrationBuilder.DropPrimaryKey(
                name: "PK_candidates",
                table: "candidates");

            migrationBuilder.DropIndex(
                name: "IX_candidates_ApplicationNo",
                table: "candidates");

            migrationBuilder.DropIndex(
                name: "IX_candidates_CurrentStageEnteredAt",
                table: "candidates");

            migrationBuilder.DropIndex(
                name: "IX_candidates_CurrentStageId",
                table: "candidates");

            migrationBuilder.DropIndex(
                name: "IX_candidates_LabourId",
                table: "candidates");

            migrationBuilder.DropIndex(
                name: "IX_candidates_OfficeId",
                table: "candidates");

            migrationBuilder.DropIndex(
                name: "IX_candidates_PassportNumber",
                table: "candidates");

            migrationBuilder.DropPrimaryKey(
                name: "PK_WorkflowTransitionRules",
                table: "WorkflowTransitionRules");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowTransitionRules_WorkflowDefinitionId1",
                table: "WorkflowTransitionRules");

            migrationBuilder.DropPrimaryKey(
                name: "PK_WorkflowStageStatuses",
                table: "WorkflowStageStatuses");

            migrationBuilder.DropPrimaryKey(
                name: "PK_workflow_stages",
                table: "workflow_stages");

            migrationBuilder.DropIndex(
                name: "IX_workflow_stages_WorkflowDefinitionId_SortOrder",
                table: "workflow_stages");

            migrationBuilder.DropPrimaryKey(
                name: "PK_workflow_snapshots",
                table: "workflow_snapshots");

            migrationBuilder.DropIndex(
                name: "IX_workflow_snapshots_CandidateId_SequenceNumber",
                table: "workflow_snapshots");

            migrationBuilder.DropPrimaryKey(
                name: "PK_workflow_events",
                table: "workflow_events");

            migrationBuilder.DropIndex(
                name: "IX_workflow_events_CandidateId_SequenceNumber",
                table: "workflow_events");

            migrationBuilder.DropIndex(
                name: "IX_workflow_events_Timestamp",
                table: "workflow_events");

            migrationBuilder.DropPrimaryKey(
                name: "PK_workflow_definitions",
                table: "workflow_definitions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_StageMandatoryFields",
                table: "StageMandatoryFields");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ParallelTrackDefinitions",
                table: "ParallelTrackDefinitions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MirrorViewRules",
                table: "MirrorViewRules");

            migrationBuilder.DropPrimaryKey(
                name: "PK_candidate_documents",
                table: "candidate_documents");

            migrationBuilder.DropColumn(
                name: "ApplicationNo",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "BiometricId",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "CurrentStageEnteredAt",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "FileNumber",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "FlightDate",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "HouseNo",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "IsOverdue",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "LastActionAt",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "LastActionLabel",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "MaritalStatus",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "NationalId",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "Occupation",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "PassportExpiryDate",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "PassportIssueDate",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "PassportType",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "Phone2",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "PlaceOfBirth",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "PlaceOfIssue",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "Qualification",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "Region",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "Religion",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "Subcity",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "Woreda",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "WorkflowDefinitionId1",
                table: "WorkflowTransitionRules");

            migrationBuilder.DropColumn(
                name: "CriticalDurationHours",
                table: "workflow_stages");

            migrationBuilder.DropColumn(
                name: "ExpectedDurationHours",
                table: "workflow_stages");

            migrationBuilder.DropColumn(
                name: "WarningDurationHours",
                table: "workflow_stages");

            migrationBuilder.RenameTable(
                name: "candidates",
                newName: "Candidates");

            migrationBuilder.RenameTable(
                name: "WorkflowTransitionRules",
                newName: "WorkflowTransitionRule");

            migrationBuilder.RenameTable(
                name: "WorkflowStageStatuses",
                newName: "WorkflowStageStatus");

            migrationBuilder.RenameTable(
                name: "workflow_stages",
                newName: "WorkflowStages");

            migrationBuilder.RenameTable(
                name: "workflow_snapshots",
                newName: "WorkflowSnapshots");

            migrationBuilder.RenameTable(
                name: "workflow_events",
                newName: "WorkflowEvents");

            migrationBuilder.RenameTable(
                name: "workflow_definitions",
                newName: "WorkflowDefinitions");

            migrationBuilder.RenameTable(
                name: "StageMandatoryFields",
                newName: "StageMandatoryField");

            migrationBuilder.RenameTable(
                name: "ParallelTrackDefinitions",
                newName: "ParallelTrackDefinition");

            migrationBuilder.RenameTable(
                name: "MirrorViewRules",
                newName: "MirrorViewRule");

            migrationBuilder.RenameTable(
                name: "candidate_documents",
                newName: "CandidateDocuments");

            migrationBuilder.RenameIndex(
                name: "IX_WorkflowTransitionRules_WorkflowDefinitionId",
                table: "WorkflowTransitionRule",
                newName: "IX_WorkflowTransitionRule_WorkflowDefinitionId");

            migrationBuilder.RenameIndex(
                name: "IX_WorkflowTransitionRules_TargetStageId",
                table: "WorkflowTransitionRule",
                newName: "IX_WorkflowTransitionRule_TargetStageId");

            migrationBuilder.RenameIndex(
                name: "IX_WorkflowTransitionRules_SourceStageId",
                table: "WorkflowTransitionRule",
                newName: "IX_WorkflowTransitionRule_SourceStageId");

            migrationBuilder.RenameIndex(
                name: "IX_WorkflowStageStatuses_WorkflowStageId",
                table: "WorkflowStageStatus",
                newName: "IX_WorkflowStageStatus_WorkflowStageId");

            migrationBuilder.RenameIndex(
                name: "IX_StageMandatoryFields_WorkflowStageId",
                table: "StageMandatoryField",
                newName: "IX_StageMandatoryField_WorkflowStageId");

            migrationBuilder.RenameIndex(
                name: "IX_StageMandatoryFields_TransitionRuleId",
                table: "StageMandatoryField",
                newName: "IX_StageMandatoryField_TransitionRuleId");

            migrationBuilder.RenameIndex(
                name: "IX_ParallelTrackDefinitions_WorkflowStageId",
                table: "ParallelTrackDefinition",
                newName: "IX_ParallelTrackDefinition_WorkflowStageId");

            migrationBuilder.RenameIndex(
                name: "IX_MirrorViewRules_WorkflowStageId",
                table: "MirrorViewRule",
                newName: "IX_MirrorViewRule_WorkflowStageId");

            migrationBuilder.RenameIndex(
                name: "IX_MirrorViewRules_TargetStageId",
                table: "MirrorViewRule",
                newName: "IX_MirrorViewRule_TargetStageId");

            migrationBuilder.RenameIndex(
                name: "IX_candidate_documents_CandidateId",
                table: "CandidateDocuments",
                newName: "IX_CandidateDocuments_CandidateId");

            migrationBuilder.AlterColumn<string>(
                name: "PassportNumber",
                table: "Candidates",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "LastName",
                table: "Candidates",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "FirstName",
                table: "Candidates",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "WorkflowStages",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "WorkflowDefinitions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "OriginalFileName",
                table: "CandidateDocuments",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                table: "CandidateDocuments",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Candidates",
                table: "Candidates",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_WorkflowTransitionRule",
                table: "WorkflowTransitionRule",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_WorkflowStageStatus",
                table: "WorkflowStageStatus",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_WorkflowStages",
                table: "WorkflowStages",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_WorkflowSnapshots",
                table: "WorkflowSnapshots",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_WorkflowEvents",
                table: "WorkflowEvents",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_WorkflowDefinitions",
                table: "WorkflowDefinitions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_StageMandatoryField",
                table: "StageMandatoryField",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ParallelTrackDefinition",
                table: "ParallelTrackDefinition",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_MirrorViewRule",
                table: "MirrorViewRule",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CandidateDocuments",
                table: "CandidateDocuments",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStages_WorkflowDefinitionId",
                table: "WorkflowStages",
                column: "WorkflowDefinitionId");

            migrationBuilder.AddForeignKey(
                name: "FK_CandidateDocuments_Candidates_CandidateId",
                table: "CandidateDocuments",
                column: "CandidateId",
                principalTable: "Candidates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MirrorViewRule_WorkflowStages_TargetStageId",
                table: "MirrorViewRule",
                column: "TargetStageId",
                principalTable: "WorkflowStages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MirrorViewRule_WorkflowStages_WorkflowStageId",
                table: "MirrorViewRule",
                column: "WorkflowStageId",
                principalTable: "WorkflowStages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ParallelTrackDefinition_WorkflowStages_WorkflowStageId",
                table: "ParallelTrackDefinition",
                column: "WorkflowStageId",
                principalTable: "WorkflowStages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StageMandatoryField_WorkflowStages_WorkflowStageId",
                table: "StageMandatoryField",
                column: "WorkflowStageId",
                principalTable: "WorkflowStages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StageMandatoryField_WorkflowTransitionRule_TransitionRuleId",
                table: "StageMandatoryField",
                column: "TransitionRuleId",
                principalTable: "WorkflowTransitionRule",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowStages_WorkflowDefinitions_WorkflowDefinitionId",
                table: "WorkflowStages",
                column: "WorkflowDefinitionId",
                principalTable: "WorkflowDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowStageStatus_WorkflowStages_WorkflowStageId",
                table: "WorkflowStageStatus",
                column: "WorkflowStageId",
                principalTable: "WorkflowStages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowTransitionRule_WorkflowDefinitions_WorkflowDefiniti~",
                table: "WorkflowTransitionRule",
                column: "WorkflowDefinitionId",
                principalTable: "WorkflowDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowTransitionRule_WorkflowStages_SourceStageId",
                table: "WorkflowTransitionRule",
                column: "SourceStageId",
                principalTable: "WorkflowStages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowTransitionRule_WorkflowStages_TargetStageId",
                table: "WorkflowTransitionRule",
                column: "TargetStageId",
                principalTable: "WorkflowStages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
