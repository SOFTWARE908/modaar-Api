using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace modaar.api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdminContractFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "IdTypeId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "NationalityId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AnnualWaterFee",
                table: "Contracts",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BrokerEmployeeId",
                table: "Contracts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ContractPlaceId",
                table: "Contracts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ContractTypeId",
                table: "Contracts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InstallmentsCount",
                table: "Contracts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LessorRepresentative_FullName",
                table: "Contracts",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LessorRepresentative_IdNumber",
                table: "Contracts",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LessorRepresentative_IdTypeId",
                table: "Contracts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LessorRepresentative_NationalityId",
                table: "Contracts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LessorRepresentative_PhoneNumber",
                table: "Contracts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentChannelIds",
                table: "Contracts",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<DateOnly>(
                name: "SignedOn",
                table: "Contracts",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantRepresentative_FullName",
                table: "Contracts",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantRepresentative_IdNumber",
                table: "Contracts",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantRepresentative_IdTypeId",
                table: "Contracts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantRepresentative_NationalityId",
                table: "Contracts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantRepresentative_PhoneNumber",
                table: "Contracts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalValue",
                table: "Contracts",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommercialRegistrationNumber",
                table: "Brokers",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyName",
                table: "Brokers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BrokerEmployees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BrokerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrokerEmployees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BrokerEmployees_Brokers_BrokerId",
                        column: x => x.BrokerId,
                        principalTable: "Brokers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LookupCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LookupCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Lookups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lookups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Lookups_LookupCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "LookupCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "LookupCategories",
                columns: new[] { "Id", "Code", "NameAr", "NameEn" },
                values: new object[,]
                {
                    { new Guid("1c000000-0000-4000-8000-000000000001"), "nationality", "الجنسية", "Nationality" },
                    { new Guid("1c000000-0000-4000-8000-000000000002"), "id_type", "نوع الهوية", "ID type" },
                    { new Guid("1c000000-0000-4000-8000-000000000003"), "contract_place", "مكان إبرام العقد", "Place of signing" },
                    { new Guid("1c000000-0000-4000-8000-000000000004"), "contract_type", "نوع العقد", "Contract type" },
                    { new Guid("1c000000-0000-4000-8000-000000000005"), "payment_channel", "قنوات الدفع", "Payment channels" }
                });

            migrationBuilder.InsertData(
                table: "Lookups",
                columns: new[] { "Id", "CategoryId", "Code", "IsActive", "NameAr", "NameEn", "SortOrder" },
                values: new object[,]
                {
                    { new Guid("1d000001-0000-4000-8000-000000000001"), new Guid("1c000000-0000-4000-8000-000000000001"), "sa", true, "المملكة العربية السعودية", "Saudi Arabia", 10 },
                    { new Guid("1d000001-0000-4000-8000-000000000002"), new Guid("1c000000-0000-4000-8000-000000000001"), "af", true, "أفغانستان", "Afghanistan", 20 },
                    { new Guid("1d000001-0000-4000-8000-000000000003"), new Guid("1c000000-0000-4000-8000-000000000001"), "al", true, "ألبانيا", "Albania", 30 },
                    { new Guid("1d000001-0000-4000-8000-000000000004"), new Guid("1c000000-0000-4000-8000-000000000001"), "dz", true, "الجزائر", "Algeria", 40 },
                    { new Guid("1d000001-0000-4000-8000-000000000005"), new Guid("1c000000-0000-4000-8000-000000000001"), "ar", true, "الأرجنتين", "Argentina", 50 },
                    { new Guid("1d000001-0000-4000-8000-000000000006"), new Guid("1c000000-0000-4000-8000-000000000001"), "am", true, "أرمينيا", "Armenia", 60 },
                    { new Guid("1d000001-0000-4000-8000-000000000007"), new Guid("1c000000-0000-4000-8000-000000000001"), "au", true, "أستراليا", "Australia", 70 },
                    { new Guid("1d000001-0000-4000-8000-000000000008"), new Guid("1c000000-0000-4000-8000-000000000001"), "at", true, "النمسا", "Austria", 80 },
                    { new Guid("1d000001-0000-4000-8000-000000000009"), new Guid("1c000000-0000-4000-8000-000000000001"), "az", true, "أذربيجان", "Azerbaijan", 90 },
                    { new Guid("1d000001-0000-4000-8000-000000000010"), new Guid("1c000000-0000-4000-8000-000000000001"), "bh", true, "البحرين", "Bahrain", 100 },
                    { new Guid("1d000001-0000-4000-8000-000000000011"), new Guid("1c000000-0000-4000-8000-000000000001"), "bd", true, "بنغلاديش", "Bangladesh", 110 },
                    { new Guid("1d000001-0000-4000-8000-000000000012"), new Guid("1c000000-0000-4000-8000-000000000001"), "be", true, "بلجيكا", "Belgium", 120 },
                    { new Guid("1d000001-0000-4000-8000-000000000013"), new Guid("1c000000-0000-4000-8000-000000000001"), "ba", true, "البوسنة والهرسك", "Bosnia and Herzegovina", 130 },
                    { new Guid("1d000001-0000-4000-8000-000000000014"), new Guid("1c000000-0000-4000-8000-000000000001"), "bf", true, "بوركينا فاسو", "Burkina Faso", 140 },
                    { new Guid("1d000001-0000-4000-8000-000000000015"), new Guid("1c000000-0000-4000-8000-000000000001"), "bi", true, "بوروندي", "Burundi", 150 },
                    { new Guid("1d000001-0000-4000-8000-000000000016"), new Guid("1c000000-0000-4000-8000-000000000001"), "cm", true, "الكاميرون", "Cameroon", 160 },
                    { new Guid("1d000001-0000-4000-8000-000000000017"), new Guid("1c000000-0000-4000-8000-000000000001"), "ca", true, "كندا", "Canada", 170 },
                    { new Guid("1d000001-0000-4000-8000-000000000018"), new Guid("1c000000-0000-4000-8000-000000000001"), "cv", true, "الرأس الأخضر", "Cape Verde", 180 },
                    { new Guid("1d000001-0000-4000-8000-000000000019"), new Guid("1c000000-0000-4000-8000-000000000001"), "td", true, "تشاد", "Chad", 190 },
                    { new Guid("1d000001-0000-4000-8000-000000000020"), new Guid("1c000000-0000-4000-8000-000000000001"), "cn", true, "الصين", "China", 200 },
                    { new Guid("1d000001-0000-4000-8000-000000000021"), new Guid("1c000000-0000-4000-8000-000000000001"), "co", true, "كولومبيا", "Colombia", 210 },
                    { new Guid("1d000001-0000-4000-8000-000000000022"), new Guid("1c000000-0000-4000-8000-000000000001"), "km", true, "جزر القمر", "Comoros", 220 },
                    { new Guid("1d000001-0000-4000-8000-000000000023"), new Guid("1c000000-0000-4000-8000-000000000001"), "cg", true, "الكونغو", "Congo", 230 },
                    { new Guid("1d000001-0000-4000-8000-000000000024"), new Guid("1c000000-0000-4000-8000-000000000001"), "cr", true, "كوستاريكا", "Costa Rica", 240 },
                    { new Guid("1d000001-0000-4000-8000-000000000025"), new Guid("1c000000-0000-4000-8000-000000000001"), "ci", true, "ساحل العاج", "Ivory Coast", 250 },
                    { new Guid("1d000001-0000-4000-8000-000000000026"), new Guid("1c000000-0000-4000-8000-000000000001"), "hr", true, "كرواتيا", "Croatia", 260 },
                    { new Guid("1d000001-0000-4000-8000-000000000027"), new Guid("1c000000-0000-4000-8000-000000000001"), "cy", true, "قبرص", "Cyprus", 270 },
                    { new Guid("1d000001-0000-4000-8000-000000000028"), new Guid("1c000000-0000-4000-8000-000000000001"), "cz", true, "التشيك", "Czech Republic", 280 },
                    { new Guid("1d000001-0000-4000-8000-000000000029"), new Guid("1c000000-0000-4000-8000-000000000001"), "dk", true, "الدنمارك", "Denmark", 290 },
                    { new Guid("1d000001-0000-4000-8000-000000000030"), new Guid("1c000000-0000-4000-8000-000000000001"), "dj", true, "جيبوتي", "Djibouti", 300 },
                    { new Guid("1d000001-0000-4000-8000-000000000031"), new Guid("1c000000-0000-4000-8000-000000000001"), "ec", true, "الإكوادور", "Ecuador", 310 },
                    { new Guid("1d000001-0000-4000-8000-000000000032"), new Guid("1c000000-0000-4000-8000-000000000001"), "eg", true, "مصر", "Egypt", 320 },
                    { new Guid("1d000001-0000-4000-8000-000000000033"), new Guid("1c000000-0000-4000-8000-000000000001"), "sv", true, "السلفادور", "El Salvador", 330 },
                    { new Guid("1d000001-0000-4000-8000-000000000034"), new Guid("1c000000-0000-4000-8000-000000000001"), "er", true, "إريتريا", "Eritrea", 340 },
                    { new Guid("1d000001-0000-4000-8000-000000000035"), new Guid("1c000000-0000-4000-8000-000000000001"), "et", true, "إثيوبيا", "Ethiopia", 350 },
                    { new Guid("1d000001-0000-4000-8000-000000000036"), new Guid("1c000000-0000-4000-8000-000000000001"), "fi", true, "فنلندا", "Finland", 360 },
                    { new Guid("1d000001-0000-4000-8000-000000000037"), new Guid("1c000000-0000-4000-8000-000000000001"), "fr", true, "فرنسا", "France", 370 },
                    { new Guid("1d000001-0000-4000-8000-000000000038"), new Guid("1c000000-0000-4000-8000-000000000001"), "gm", true, "غامبيا", "Gambia", 380 },
                    { new Guid("1d000001-0000-4000-8000-000000000039"), new Guid("1c000000-0000-4000-8000-000000000001"), "ge", true, "جورجيا", "Georgia", 390 },
                    { new Guid("1d000001-0000-4000-8000-000000000040"), new Guid("1c000000-0000-4000-8000-000000000001"), "de", true, "ألمانيا", "Germany", 400 },
                    { new Guid("1d000001-0000-4000-8000-000000000041"), new Guid("1c000000-0000-4000-8000-000000000001"), "gh", true, "غانا", "Ghana", 410 },
                    { new Guid("1d000001-0000-4000-8000-000000000042"), new Guid("1c000000-0000-4000-8000-000000000001"), "gr", true, "اليونان", "Greece", 420 },
                    { new Guid("1d000001-0000-4000-8000-000000000043"), new Guid("1c000000-0000-4000-8000-000000000001"), "gn", true, "غينيا", "Guinea", 430 },
                    { new Guid("1d000001-0000-4000-8000-000000000044"), new Guid("1c000000-0000-4000-8000-000000000001"), "hu", true, "المجر", "Hungary", 440 },
                    { new Guid("1d000001-0000-4000-8000-000000000045"), new Guid("1c000000-0000-4000-8000-000000000001"), "is", true, "آيسلندا", "Iceland", 450 },
                    { new Guid("1d000001-0000-4000-8000-000000000046"), new Guid("1c000000-0000-4000-8000-000000000001"), "in", true, "الهند", "India", 460 },
                    { new Guid("1d000001-0000-4000-8000-000000000047"), new Guid("1c000000-0000-4000-8000-000000000001"), "id", true, "إندونيسيا", "Indonesia", 470 },
                    { new Guid("1d000001-0000-4000-8000-000000000048"), new Guid("1c000000-0000-4000-8000-000000000001"), "ir", true, "إيران", "Iran", 480 },
                    { new Guid("1d000001-0000-4000-8000-000000000049"), new Guid("1c000000-0000-4000-8000-000000000001"), "iq", true, "العراق", "Iraq", 490 },
                    { new Guid("1d000001-0000-4000-8000-000000000050"), new Guid("1c000000-0000-4000-8000-000000000001"), "ie", true, "أيرلندا", "Ireland", 500 },
                    { new Guid("1d000001-0000-4000-8000-000000000051"), new Guid("1c000000-0000-4000-8000-000000000001"), "it", true, "إيطاليا", "Italy", 510 },
                    { new Guid("1d000001-0000-4000-8000-000000000052"), new Guid("1c000000-0000-4000-8000-000000000001"), "jm", true, "جامايكا", "Jamaica", 520 },
                    { new Guid("1d000001-0000-4000-8000-000000000053"), new Guid("1c000000-0000-4000-8000-000000000001"), "jp", true, "اليابان", "Japan", 530 },
                    { new Guid("1d000001-0000-4000-8000-000000000054"), new Guid("1c000000-0000-4000-8000-000000000001"), "jo", true, "الأردن", "Jordan", 540 },
                    { new Guid("1d000001-0000-4000-8000-000000000055"), new Guid("1c000000-0000-4000-8000-000000000001"), "kz", true, "كازاخستان", "Kazakhstan", 550 },
                    { new Guid("1d000001-0000-4000-8000-000000000056"), new Guid("1c000000-0000-4000-8000-000000000001"), "ke", true, "كينيا", "Kenya", 560 },
                    { new Guid("1d000001-0000-4000-8000-000000000057"), new Guid("1c000000-0000-4000-8000-000000000001"), "kw", true, "الكويت", "Kuwait", 570 },
                    { new Guid("1d000001-0000-4000-8000-000000000058"), new Guid("1c000000-0000-4000-8000-000000000001"), "kg", true, "قيرغيزستان", "Kyrgyzstan", 580 },
                    { new Guid("1d000001-0000-4000-8000-000000000059"), new Guid("1c000000-0000-4000-8000-000000000001"), "lb", true, "لبنان", "Lebanon", 590 },
                    { new Guid("1d000001-0000-4000-8000-000000000060"), new Guid("1c000000-0000-4000-8000-000000000001"), "ly", true, "ليبيا", "Libya", 600 },
                    { new Guid("1d000001-0000-4000-8000-000000000061"), new Guid("1c000000-0000-4000-8000-000000000001"), "mg", true, "مدغشقر", "Madagascar", 610 },
                    { new Guid("1d000001-0000-4000-8000-000000000062"), new Guid("1c000000-0000-4000-8000-000000000001"), "my", true, "ماليزيا", "Malaysia", 620 },
                    { new Guid("1d000001-0000-4000-8000-000000000063"), new Guid("1c000000-0000-4000-8000-000000000001"), "mv", true, "جزر المالديف", "Maldives", 630 },
                    { new Guid("1d000001-0000-4000-8000-000000000064"), new Guid("1c000000-0000-4000-8000-000000000001"), "mr", true, "موريتانيا", "Mauritania", 640 },
                    { new Guid("1d000001-0000-4000-8000-000000000065"), new Guid("1c000000-0000-4000-8000-000000000001"), "mu", true, "موريشيوس", "Mauritius", 650 },
                    { new Guid("1d000001-0000-4000-8000-000000000066"), new Guid("1c000000-0000-4000-8000-000000000001"), "ma", true, "المغرب", "Morocco", 660 },
                    { new Guid("1d000001-0000-4000-8000-000000000067"), new Guid("1c000000-0000-4000-8000-000000000001"), "mz", true, "موزمبيق", "Mozambique", 670 },
                    { new Guid("1d000001-0000-4000-8000-000000000068"), new Guid("1c000000-0000-4000-8000-000000000001"), "np", true, "نيبال", "Nepal", 680 },
                    { new Guid("1d000001-0000-4000-8000-000000000069"), new Guid("1c000000-0000-4000-8000-000000000001"), "nl", true, "هولندا", "Netherlands", 690 },
                    { new Guid("1d000001-0000-4000-8000-000000000070"), new Guid("1c000000-0000-4000-8000-000000000001"), "nz", true, "نيوزيلندا", "New Zealand", 700 },
                    { new Guid("1d000001-0000-4000-8000-000000000071"), new Guid("1c000000-0000-4000-8000-000000000001"), "ne", true, "النيجر", "Niger", 710 },
                    { new Guid("1d000001-0000-4000-8000-000000000072"), new Guid("1c000000-0000-4000-8000-000000000001"), "ng", true, "نيجيريا", "Nigeria", 720 },
                    { new Guid("1d000001-0000-4000-8000-000000000073"), new Guid("1c000000-0000-4000-8000-000000000001"), "no", true, "النرويج", "Norway", 730 },
                    { new Guid("1d000001-0000-4000-8000-000000000074"), new Guid("1c000000-0000-4000-8000-000000000001"), "om", true, "عُمان", "Oman", 740 },
                    { new Guid("1d000001-0000-4000-8000-000000000075"), new Guid("1c000000-0000-4000-8000-000000000001"), "pk", true, "باكستان", "Pakistan", 750 },
                    { new Guid("1d000001-0000-4000-8000-000000000076"), new Guid("1c000000-0000-4000-8000-000000000001"), "ps", true, "فلسطين", "Palestine", 760 },
                    { new Guid("1d000001-0000-4000-8000-000000000077"), new Guid("1c000000-0000-4000-8000-000000000001"), "pa", true, "بنما", "Panama", 770 },
                    { new Guid("1d000001-0000-4000-8000-000000000078"), new Guid("1c000000-0000-4000-8000-000000000001"), "ph", true, "الفلبين", "Philippines", 780 },
                    { new Guid("1d000001-0000-4000-8000-000000000079"), new Guid("1c000000-0000-4000-8000-000000000001"), "pl", true, "بولندا", "Poland", 790 },
                    { new Guid("1d000001-0000-4000-8000-000000000080"), new Guid("1c000000-0000-4000-8000-000000000001"), "pt", true, "البرتغال", "Portugal", 800 },
                    { new Guid("1d000001-0000-4000-8000-000000000081"), new Guid("1c000000-0000-4000-8000-000000000001"), "qa", true, "قطر", "Qatar", 810 },
                    { new Guid("1d000001-0000-4000-8000-000000000082"), new Guid("1c000000-0000-4000-8000-000000000001"), "ro", true, "رومانيا", "Romania", 820 },
                    { new Guid("1d000001-0000-4000-8000-000000000083"), new Guid("1c000000-0000-4000-8000-000000000001"), "ru", true, "روسيا", "Russia", 830 },
                    { new Guid("1d000001-0000-4000-8000-000000000084"), new Guid("1c000000-0000-4000-8000-000000000001"), "rw", true, "رواندا", "Rwanda", 840 },
                    { new Guid("1d000001-0000-4000-8000-000000000085"), new Guid("1c000000-0000-4000-8000-000000000001"), "sn", true, "السنغال", "Senegal", 850 },
                    { new Guid("1d000001-0000-4000-8000-000000000086"), new Guid("1c000000-0000-4000-8000-000000000001"), "rs", true, "صربيا", "Serbia", 860 },
                    { new Guid("1d000001-0000-4000-8000-000000000087"), new Guid("1c000000-0000-4000-8000-000000000001"), "sg", true, "سنغافورة", "Singapore", 870 },
                    { new Guid("1d000001-0000-4000-8000-000000000088"), new Guid("1c000000-0000-4000-8000-000000000001"), "sk", true, "سلوفاكيا", "Slovakia", 880 },
                    { new Guid("1d000001-0000-4000-8000-000000000089"), new Guid("1c000000-0000-4000-8000-000000000001"), "si", true, "سلوفينيا", "Slovenia", 890 },
                    { new Guid("1d000001-0000-4000-8000-000000000090"), new Guid("1c000000-0000-4000-8000-000000000001"), "sb", true, "جزر سليمان", "Solomon Islands", 900 },
                    { new Guid("1d000001-0000-4000-8000-000000000091"), new Guid("1c000000-0000-4000-8000-000000000001"), "so", true, "الصومال", "Somalia", 910 },
                    { new Guid("1d000001-0000-4000-8000-000000000092"), new Guid("1c000000-0000-4000-8000-000000000001"), "za", true, "جنوب أفريقيا", "South Africa", 920 },
                    { new Guid("1d000001-0000-4000-8000-000000000093"), new Guid("1c000000-0000-4000-8000-000000000001"), "kr", true, "كوريا الجنوبية", "South Korea", 930 },
                    { new Guid("1d000001-0000-4000-8000-000000000094"), new Guid("1c000000-0000-4000-8000-000000000001"), "es", true, "إسبانيا", "Spain", 940 },
                    { new Guid("1d000001-0000-4000-8000-000000000095"), new Guid("1c000000-0000-4000-8000-000000000001"), "lk", true, "سريلانكا", "Sri Lanka", 950 },
                    { new Guid("1d000001-0000-4000-8000-000000000096"), new Guid("1c000000-0000-4000-8000-000000000001"), "sd", true, "السودان", "Sudan", 960 },
                    { new Guid("1d000001-0000-4000-8000-000000000097"), new Guid("1c000000-0000-4000-8000-000000000001"), "se", true, "السويد", "Sweden", 970 },
                    { new Guid("1d000001-0000-4000-8000-000000000098"), new Guid("1c000000-0000-4000-8000-000000000001"), "ch", true, "سويسرا", "Switzerland", 980 },
                    { new Guid("1d000001-0000-4000-8000-000000000099"), new Guid("1c000000-0000-4000-8000-000000000001"), "sy", true, "سوريا", "Syria", 990 },
                    { new Guid("1d000001-0000-4000-8000-000000000100"), new Guid("1c000000-0000-4000-8000-000000000001"), "tj", true, "طاجيكستان", "Tajikistan", 1000 },
                    { new Guid("1d000001-0000-4000-8000-000000000101"), new Guid("1c000000-0000-4000-8000-000000000001"), "th", true, "تايلاند", "Thailand", 1010 },
                    { new Guid("1d000001-0000-4000-8000-000000000102"), new Guid("1c000000-0000-4000-8000-000000000001"), "tn", true, "تونس", "Tunisia", 1020 },
                    { new Guid("1d000001-0000-4000-8000-000000000103"), new Guid("1c000000-0000-4000-8000-000000000001"), "tr", true, "تركيا", "Turkey", 1030 },
                    { new Guid("1d000001-0000-4000-8000-000000000104"), new Guid("1c000000-0000-4000-8000-000000000001"), "ug", true, "أوغندا", "Uganda", 1040 },
                    { new Guid("1d000001-0000-4000-8000-000000000105"), new Guid("1c000000-0000-4000-8000-000000000001"), "ua", true, "أوكرانيا", "Ukraine", 1050 },
                    { new Guid("1d000001-0000-4000-8000-000000000106"), new Guid("1c000000-0000-4000-8000-000000000001"), "ae", true, "الإمارات العربية المتحدة", "United Arab Emirates", 1060 },
                    { new Guid("1d000001-0000-4000-8000-000000000107"), new Guid("1c000000-0000-4000-8000-000000000001"), "gb", true, "المملكة المتحدة", "United Kingdom", 1070 },
                    { new Guid("1d000001-0000-4000-8000-000000000108"), new Guid("1c000000-0000-4000-8000-000000000001"), "us", true, "الولايات المتحدة الأمريكية", "United States", 1080 },
                    { new Guid("1d000001-0000-4000-8000-000000000109"), new Guid("1c000000-0000-4000-8000-000000000001"), "uy", true, "أوروغواي", "Uruguay", 1090 },
                    { new Guid("1d000001-0000-4000-8000-000000000110"), new Guid("1c000000-0000-4000-8000-000000000001"), "uz", true, "أوزبكستان", "Uzbekistan", 1100 },
                    { new Guid("1d000001-0000-4000-8000-000000000111"), new Guid("1c000000-0000-4000-8000-000000000001"), "vn", true, "فيتنام", "Vietnam", 1110 },
                    { new Guid("1d000001-0000-4000-8000-000000000112"), new Guid("1c000000-0000-4000-8000-000000000001"), "ye", true, "اليمن", "Yemen", 1120 },
                    { new Guid("1d000002-0000-4000-8000-000000000001"), new Guid("1c000000-0000-4000-8000-000000000002"), "national_id", true, "هوية وطنية", "National ID", 10 },
                    { new Guid("1d000002-0000-4000-8000-000000000002"), new Guid("1c000000-0000-4000-8000-000000000002"), "premium_residency", true, "إقامة مميزة", "Premium residency", 20 },
                    { new Guid("1d000002-0000-4000-8000-000000000003"), new Guid("1c000000-0000-4000-8000-000000000002"), "resident_id", true, "هوية مقيم", "Resident ID", 30 },
                    { new Guid("1d000002-0000-4000-8000-000000000004"), new Guid("1c000000-0000-4000-8000-000000000002"), "gcc_id", true, "هوية مواطني دول مجلس التعاون الخليجي", "GCC citizen ID", 40 },
                    { new Guid("1d000002-0000-4000-8000-000000000005"), new Guid("1c000000-0000-4000-8000-000000000002"), "passport", true, "جواز سفر", "Passport", 50 },
                    { new Guid("1d000002-0000-4000-8000-000000000006"), new Guid("1c000000-0000-4000-8000-000000000002"), "other", true, "أخرى", "Other", 60 },
                    { new Guid("1d000003-0000-4000-8000-000000000001"), new Guid("1c000000-0000-4000-8000-000000000003"), "riyadh", true, "الرياض", "Riyadh", 10 },
                    { new Guid("1d000003-0000-4000-8000-000000000002"), new Guid("1c000000-0000-4000-8000-000000000003"), "other", true, "أخرى", "Other", 20 },
                    { new Guid("1d000004-0000-4000-8000-000000000001"), new Guid("1c000000-0000-4000-8000-000000000004"), "new", true, "جديد", "New", 10 },
                    { new Guid("1d000004-0000-4000-8000-000000000002"), new Guid("1c000000-0000-4000-8000-000000000004"), "renewal", true, "تجديد", "Renewal", 20 },
                    { new Guid("1d000005-0000-4000-8000-000000000001"), new Guid("1c000000-0000-4000-8000-000000000005"), "mada", true, "مدى", "Mada", 10 },
                    { new Guid("1d000005-0000-4000-8000-000000000002"), new Guid("1c000000-0000-4000-8000-000000000005"), "sadad", true, "سداد", "SADAD", 20 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_IdTypeId",
                table: "Users",
                column: "IdTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_NationalityId",
                table: "Users",
                column: "NationalityId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_BrokerEmployeeId",
                table: "Contracts",
                column: "BrokerEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_ContractPlaceId",
                table: "Contracts",
                column: "ContractPlaceId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_ContractTypeId",
                table: "Contracts",
                column: "ContractTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_BrokerEmployees_BrokerId_IsActive",
                table: "BrokerEmployees",
                columns: new[] { "BrokerId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_LookupCategories_Code",
                table: "LookupCategories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lookups_CategoryId_Code",
                table: "Lookups",
                columns: new[] { "CategoryId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lookups_CategoryId_IsActive_SortOrder",
                table: "Lookups",
                columns: new[] { "CategoryId", "IsActive", "SortOrder" });

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_BrokerEmployees_BrokerEmployeeId",
                table: "Contracts",
                column: "BrokerEmployeeId",
                principalTable: "BrokerEmployees",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_Lookups_ContractPlaceId",
                table: "Contracts",
                column: "ContractPlaceId",
                principalTable: "Lookups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_Lookups_ContractTypeId",
                table: "Contracts",
                column: "ContractTypeId",
                principalTable: "Lookups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Lookups_IdTypeId",
                table: "Users",
                column: "IdTypeId",
                principalTable: "Lookups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Lookups_NationalityId",
                table: "Users",
                column: "NationalityId",
                principalTable: "Lookups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Accounts whose nationality text already names one of the countries get linked to it.
            migrationBuilder.Sql(@"
UPDATE u SET u.NationalityId = l.Id
FROM Users u
JOIN Lookups l ON l.CategoryId = '1c000000-0000-4000-8000-000000000001'
             AND LTRIM(RTRIM(u.Nationality)) IN (l.NameAr, l.NameEn)
WHERE u.NationalityId IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_BrokerEmployees_BrokerEmployeeId",
                table: "Contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_Lookups_ContractPlaceId",
                table: "Contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_Lookups_ContractTypeId",
                table: "Contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Lookups_IdTypeId",
                table: "Users");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Lookups_NationalityId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "BrokerEmployees");

            migrationBuilder.DropTable(
                name: "Lookups");

            migrationBuilder.DropTable(
                name: "LookupCategories");

            migrationBuilder.DropIndex(
                name: "IX_Users_IdTypeId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_NationalityId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_BrokerEmployeeId",
                table: "Contracts");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_ContractPlaceId",
                table: "Contracts");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_ContractTypeId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "IdTypeId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NationalityId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AnnualWaterFee",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "BrokerEmployeeId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "ContractPlaceId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "ContractTypeId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "InstallmentsCount",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "LessorRepresentative_FullName",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "LessorRepresentative_IdNumber",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "LessorRepresentative_IdTypeId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "LessorRepresentative_NationalityId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "LessorRepresentative_PhoneNumber",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "PaymentChannelIds",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "SignedOn",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "TenantRepresentative_FullName",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "TenantRepresentative_IdNumber",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "TenantRepresentative_IdTypeId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "TenantRepresentative_NationalityId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "TenantRepresentative_PhoneNumber",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "TotalValue",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "CommercialRegistrationNumber",
                table: "Brokers");

            migrationBuilder.DropColumn(
                name: "CompanyName",
                table: "Brokers");
        }
    }
}
