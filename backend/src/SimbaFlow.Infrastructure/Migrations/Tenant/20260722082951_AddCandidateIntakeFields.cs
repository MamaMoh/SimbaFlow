using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimbaFlow.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddCandidateIntakeFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "application_no",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "arabic_level",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "biometric_id",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "certificate_no",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "certified_date",
                table: "candidates",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "coc_center_name",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contact_person2",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contact_phone2",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contract_no",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contract_period",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cooking_level",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "english_level",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "experience_abroad_years",
                table: "candidates",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "file_no",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "height",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "house_no",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "local_full_name",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "marital_status",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "medical_place",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "monthly_salary",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "national_id",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "number_of_children",
                table: "candidates",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "occupation",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "passport_expiry_date",
                table: "candidates",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "passport_issue_date",
                table: "candidates",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "passport_place_of_issue",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "passport_type",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "place_of_birth",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "qualification",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reference_no",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "region",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "relative_birth_date",
                table: "candidates",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "relative_city",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "relative_gender",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "relative_house_no",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "relative_kinship",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "relative_name",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "relative_phone",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "relative_region",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "relative_subcity",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "relative_woreda",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "religion",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "remark",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "signed_on",
                table: "candidates",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "skill_babysitting",
                table: "candidates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "skill_child_care",
                table: "candidates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "skill_cleaning",
                table: "candidates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "skill_cooking",
                table: "candidates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "skill_ironing",
                table: "candidates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "skill_sewing",
                table: "candidates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "skill_washing",
                table: "candidates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "sticker_visa_no",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "subcity",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "wakala_no",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "weight",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "woreda",
                table: "candidates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "works_in",
                table: "candidates",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "application_no",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "arabic_level",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "biometric_id",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "certificate_no",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "certified_date",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "coc_center_name",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "contact_person2",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "contact_phone2",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "contract_no",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "contract_period",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "cooking_level",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "english_level",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "experience_abroad_years",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "file_no",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "height",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "house_no",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "local_full_name",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "marital_status",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "medical_place",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "monthly_salary",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "national_id",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "number_of_children",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "occupation",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "passport_expiry_date",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "passport_issue_date",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "passport_place_of_issue",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "passport_type",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "place_of_birth",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "qualification",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "reference_no",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "region",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "relative_birth_date",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "relative_city",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "relative_gender",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "relative_house_no",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "relative_kinship",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "relative_name",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "relative_phone",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "relative_region",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "relative_subcity",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "relative_woreda",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "religion",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "remark",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "signed_on",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "skill_babysitting",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "skill_child_care",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "skill_cleaning",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "skill_cooking",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "skill_ironing",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "skill_sewing",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "skill_washing",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "sticker_visa_no",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "subcity",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "wakala_no",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "weight",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "woreda",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "works_in",
                table: "candidates");
        }
    }
}
