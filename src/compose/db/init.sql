-- Create database for dds
CREATE database dds;
CREATE USER dds_user WITH ENCRYPTED PASSWORD 'dds_password';
GRANT ALL PRIVILEGES ON database dds TO dds_user;

-- Create database for instrumentation
CREATE database dds_instrumentation;
CREATE USER dds_instrumentation_user WITH ENCRYPTED PASSWORD 'dds_instrumentation_password';
GRANT ALL PRIVILEGES ON database dds_instrumentation TO dds_instrumentation_user;