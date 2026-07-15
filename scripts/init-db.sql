-- SimbaFlow Database Initialization
-- This script runs once when the PostgreSQL container is first created.

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- Ensure the application user has schema creation privileges (for tenant provisioning)
GRANT CREATE ON DATABASE simbaflow TO simbaflow;
