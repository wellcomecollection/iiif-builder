﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Wellcome.Dds.AssetDomainRepositories;

#nullable disable

namespace Wellcome.Dds.AssetDomainRepositories.Migrations
{
    [DbContext(typeof(DdsInstrumentationContext))]
    [Migration("20240424082203_AddIngestJobStartedField")]
    partial class AddIngestJobStartedField
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "6.0.6")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("Wellcome.Dds.AssetDomain.Dlcs.Ingest.DlcsBatch", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasColumnName("id");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<int>("BatchSize")
                        .HasColumnType("integer")
                        .HasColumnName("batch_size");

                    b.Property<int?>("ContentLength")
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("integer")
                        .HasColumnName("content_length");

                    b.Property<int>("DlcsIngestJobId")
                        .HasColumnType("integer")
                        .HasColumnName("dlcs_ingest_job_id");

                    b.Property<int>("ErrorCode")
                        .HasColumnType("integer")
                        .HasColumnName("error_code");

                    b.Property<string>("ErrorText")
                        .HasColumnType("text")
                        .HasColumnName("error_text");

                    b.Property<DateTime?>("Finished")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("finished");

                    b.Property<string>("RequestBody")
                        .HasColumnType("text")
                        .HasColumnName("request_body");

                    b.Property<DateTime?>("RequestSent")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("request_sent");

                    b.Property<string>("ResponseBody")
                        .HasColumnType("text")
                        .HasColumnName("response_body");

                    b.HasKey("Id")
                        .HasName("pk_dlcs_batches");

                    b.HasIndex("DlcsIngestJobId")
                        .HasDatabaseName("ix_dlcs_batches_dlcs_ingest_job_id");

                    b.ToTable("dlcs_batches", (string)null);
                });

            modelBuilder.Entity("Wellcome.Dds.AssetDomain.Dlcs.Ingest.DlcsIngestJob", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasColumnName("id");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<string>("AssetType")
                        .HasColumnType("text")
                        .HasColumnName("asset_type");

                    b.Property<DateTime>("Created")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created");

                    b.Property<string>("Data")
                        .HasColumnType("text")
                        .HasColumnName("data");

                    b.Property<DateTime?>("EndProcessed")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("end_processed");

                    b.Property<string>("Identifier")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("identifier");

                    b.Property<int>("ImageCount")
                        .HasColumnType("integer")
                        .HasColumnName("image_count");

                    b.Property<string>("IssuePart")
                        .HasColumnType("text")
                        .HasColumnName("issue_part");

                    b.Property<string>("Label")
                        .HasColumnType("text")
                        .HasColumnName("label");

                    b.Property<int>("ReadyImageCount")
                        .HasColumnType("integer")
                        .HasColumnName("ready_image_count");

                    b.Property<int>("SequenceIndex")
                        .HasColumnType("integer")
                        .HasColumnName("sequence_index");

                    b.Property<DateTime?>("StartProcessed")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("start_processed");

                    b.Property<bool>("Succeeded")
                        .HasColumnType("boolean")
                        .HasColumnName("succeeded");

                    b.Property<string>("VolumePart")
                        .HasColumnType("text")
                        .HasColumnName("volume_part");

                    b.HasKey("Id")
                        .HasName("pk_dlcs_ingest_jobs");

                    b.HasIndex("Created")
                        .HasDatabaseName("ix_dlcs_ingest_jobs_created");

                    b.HasIndex("Identifier")
                        .HasDatabaseName("ix_dlcs_ingest_jobs_identifier");

                    b.ToTable("dlcs_ingest_jobs", (string)null);
                });

            modelBuilder.Entity("Wellcome.Dds.AssetDomain.Dlcs.Ingest.IngestAction", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasColumnName("id");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<string>("Action")
                        .HasColumnType("text")
                        .HasColumnName("action");

                    b.Property<string>("Description")
                        .HasColumnType("text")
                        .HasColumnName("description");

                    b.Property<int?>("JobId")
                        .HasColumnType("integer")
                        .HasColumnName("job_id");

                    b.Property<string>("ManifestationId")
                        .HasColumnType("text")
                        .HasColumnName("manifestation_id");

                    b.Property<DateTime>("Performed")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("performed");

                    b.Property<string>("Username")
                        .HasColumnType("text")
                        .HasColumnName("username");

                    b.HasKey("Id")
                        .HasName("pk_ingest_actions");

                    b.ToTable("ingest_actions", (string)null);
                });

            modelBuilder.Entity("Wellcome.Dds.AssetDomain.Workflow.WorkflowJob", b =>
                {
                    b.Property<string>("Identifier")
                        .HasColumnType("text")
                        .HasColumnName("identifier");

                    b.Property<int>("AnnosAlreadyOnDisk")
                        .HasColumnType("integer")
                        .HasColumnName("annos_already_on_disk");

                    b.Property<int>("AnnosBuilt")
                        .HasColumnType("integer")
                        .HasColumnName("annos_built");

                    b.Property<DateTime?>("Created")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created");

                    b.Property<int>("DlcsJobCount")
                        .HasColumnType("integer")
                        .HasColumnName("dlcs_job_count");

                    b.Property<string>("Error")
                        .HasColumnType("text")
                        .HasColumnName("error");

                    b.Property<int>("ExpectedTexts")
                        .HasColumnType("integer")
                        .HasColumnName("expected_texts");

                    b.Property<bool>("Expedite")
                        .HasColumnType("boolean")
                        .HasColumnName("expedite");

                    b.Property<bool>("Finished")
                        .HasColumnType("boolean")
                        .HasColumnName("finished");

                    b.Property<int>("FirstDlcsJobId")
                        .HasColumnType("integer")
                        .HasColumnName("first_dlcs_job_id");

                    b.Property<bool>("FlushCache")
                        .HasColumnType("boolean")
                        .HasColumnName("flush_cache");

                    b.Property<bool>("ForceTextRebuild")
                        .HasColumnType("boolean")
                        .HasColumnName("force_text_rebuild");

                    b.Property<DateTime?>("IngestJobStarted")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("ingest_job_started");

                    b.Property<long>("PackageBuildTime")
                        .HasColumnType("bigint")
                        .HasColumnName("package_build_time");

                    b.Property<DateTime?>("Taken")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("taken");

                    b.Property<long>("TextAndAnnoBuildTime")
                        .HasColumnType("bigint")
                        .HasColumnName("text_and_anno_build_time");

                    b.Property<int>("TextPages")
                        .HasColumnType("integer")
                        .HasColumnName("text_pages");

                    b.Property<int>("TextsAlreadyOnDisk")
                        .HasColumnType("integer")
                        .HasColumnName("texts_already_on_disk");

                    b.Property<int>("TextsBuilt")
                        .HasColumnType("integer")
                        .HasColumnName("texts_built");

                    b.Property<int>("TimeSpentOnTextPages")
                        .HasColumnType("integer")
                        .HasColumnName("time_spent_on_text_pages");

                    b.Property<long>("TotalTime")
                        .HasColumnType("bigint")
                        .HasColumnName("total_time");

                    b.Property<bool>("Waiting")
                        .HasColumnType("boolean")
                        .HasColumnName("waiting");

                    b.Property<int>("Words")
                        .HasColumnType("integer")
                        .HasColumnName("words");

                    b.Property<int?>("WorkflowOptions")
                        .HasColumnType("integer")
                        .HasColumnName("workflow_options");

                    b.HasKey("Identifier")
                        .HasName("pk_workflow_jobs");

                    b.HasIndex("Created")
                        .HasDatabaseName("ix_workflow_jobs_created");

                    b.HasIndex("Expedite")
                        .HasDatabaseName("ix_workflow_jobs_expedite");

                    b.ToTable("workflow_jobs", (string)null);
                });

            modelBuilder.Entity("Wellcome.Dds.AssetDomainRepositories.Control.ControlFlow", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasColumnName("id");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<DateTime>("CreatedOn")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_on");

                    b.Property<DateTime?>("Heartbeat")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("heartbeat");

                    b.Property<DateTime?>("StoppedOn")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("stopped_on");

                    b.HasKey("Id")
                        .HasName("pk_control_flows");

                    b.ToTable("control_flows", (string)null);
                });

            modelBuilder.Entity("Wellcome.Dds.AssetDomain.Dlcs.Ingest.DlcsBatch", b =>
                {
                    b.HasOne("Wellcome.Dds.AssetDomain.Dlcs.Ingest.DlcsIngestJob", null)
                        .WithMany("DlcsBatches")
                        .HasForeignKey("DlcsIngestJobId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_dlcs_batches_dlcs_ingest_jobs_dlcs_ingest_job_id");
                });

            modelBuilder.Entity("Wellcome.Dds.AssetDomain.Dlcs.Ingest.DlcsIngestJob", b =>
                {
                    b.Navigation("DlcsBatches");
                });
#pragma warning restore 612, 618
        }
    }
}