using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyCotWeb.Migrations
{
    /// <inheritdoc />
    public partial class CreateIdentitySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK__NguoiTha__87C30EB96F455638",
                table: "NguoiThan");

            migrationBuilder.AlterColumn<string>(
                name: "LoSo",
                table: "ViTri",
                type: "nchar(10)",
                fixedLength: true,
                maxLength: 10,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nchar(10)",
                oldFixedLength: true,
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Lau",
                table: "ViTri",
                type: "nchar(10)",
                fixedLength: true,
                maxLength: 10,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nchar(10)",
                oldFixedLength: true,
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "NgayKetThuc",
                table: "Cot",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "NgayBatDau",
                table: "Cot",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_NguoiThan",
                table: "NguoiThan",
                column: "IDNguoiThan");

            migrationBuilder.CreateTable(
                name: "TinhTrang",
                columns: table => new
                {
                    IDTinhTrang = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenTinhTrang = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TinhTrang", x => x.IDTinhTrang);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ViTri_IDTinhTrang",
                table: "ViTri",
                column: "IDTinhTrang");

            migrationBuilder.CreateIndex(
                name: "IX_Cot_IDNguoiThan",
                table: "Cot",
                column: "IDNguoiThan");

            migrationBuilder.CreateIndex(
                name: "IX_Cot_IDViTri",
                table: "Cot",
                column: "IDViTri",
                unique: true,
                filter: "[IDViTri] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Cot_NguoiThan_IDNguoiThan",
                table: "Cot",
                column: "IDNguoiThan",
                principalTable: "NguoiThan",
                principalColumn: "IDNguoiThan");

            migrationBuilder.AddForeignKey(
                name: "FK_Cot_ViTri_IDViTri",
                table: "Cot",
                column: "IDViTri",
                principalTable: "ViTri",
                principalColumn: "IDViTri");

            migrationBuilder.AddForeignKey(
                name: "FK_ViTri_TinhTrang",
                table: "ViTri",
                column: "IDTinhTrang",
                principalTable: "TinhTrang",
                principalColumn: "IDTinhTrang");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cot_NguoiThan_IDNguoiThan",
                table: "Cot");

            migrationBuilder.DropForeignKey(
                name: "FK_Cot_ViTri_IDViTri",
                table: "Cot");

            migrationBuilder.DropForeignKey(
                name: "FK_ViTri_TinhTrang",
                table: "ViTri");

            migrationBuilder.DropTable(
                name: "TinhTrang");

            migrationBuilder.DropIndex(
                name: "IX_ViTri_IDTinhTrang",
                table: "ViTri");

            migrationBuilder.DropPrimaryKey(
                name: "PK_NguoiThan",
                table: "NguoiThan");

            migrationBuilder.DropIndex(
                name: "IX_Cot_IDNguoiThan",
                table: "Cot");

            migrationBuilder.DropIndex(
                name: "IX_Cot_IDViTri",
                table: "Cot");

            migrationBuilder.AlterColumn<string>(
                name: "LoSo",
                table: "ViTri",
                type: "nchar(10)",
                fixedLength: true,
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nchar(10)",
                oldFixedLength: true,
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<string>(
                name: "Lau",
                table: "ViTri",
                type: "nchar(10)",
                fixedLength: true,
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nchar(10)",
                oldFixedLength: true,
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "NgayKetThuc",
                table: "Cot",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "NgayBatDau",
                table: "Cot",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK__NguoiTha__87C30EB96F455638",
                table: "NguoiThan",
                column: "IDNguoiThan");
        }
    }
}
