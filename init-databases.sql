-- Create databases for each service
-- The default 'postgres' database already exists; we create service-specific ones here.
SELECT 'CREATE DATABASE shortening_db'
    WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'shortening_db')\gexec

SELECT 'CREATE DATABASE analytics_db'
    WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'analytics_db')\gexec
