#!/bin/bash

set -e

echo "Enhanced Database Setup for Pipeline Designer"

# Check if PostgreSQL is installed and running
if ! command -v psql &> /dev/null; then
    echo "PostgreSQL is not installed. Using SQLite database instead."
    echo "The application will use SQLite database (pipeline.db)"
    exit 0
fi

echo "PostgreSQL is installed."

# Check if PostgreSQL service is running
if ! systemctl is-active --quiet postgresql; then
    echo "Starting PostgreSQL service..."
    sudo systemctl start postgresql
fi

echo "PostgreSQL service is running."

# Remove existing database if it exists
if sudo -u postgres psql -lqt | cut -d \| -f 1 | grep -qw pipeline_designer; then
    echo "Recreating existing database 'pipeline_designer'..."
    sudo -u postgres psql -c "DROP DATABASE IF EXISTS pipeline_designer;"
fi

# Remove existing user if it exists
if sudo -u postgres psql -t -c "SELECT 1 FROM pg_roles WHERE rolname='pipeline'" | grep -q 1; then
    echo "Recreating existing user 'pipeline'..."
    sudo -u postgres psql -c "DROP USER IF EXISTS pipeline;"
fi

echo "Creating database 'pipeline_designer'..."
sudo -u postgres createdb pipeline_designer

echo "Creating user 'pipeline'..."
sudo -u postgres psql -c "CREATE USER pipeline WITH PASSWORD 'password';"

echo "Granting privileges..."
sudo -u postgres psql -c "GRANT ALL PRIVILEGES ON DATABASE pipeline_designer TO pipeline;"
sudo -u postgres psql -d pipeline_designer -c "GRANT ALL ON SCHEMA public TO pipeline;"

echo "Database setup complete!"
echo ""
echo "Database connection details:"
echo "  Host: localhost"
echo "  Database: pipeline_designer"
echo "  Username: pipeline"
echo "  Password: password"
echo ""
echo "You can now run the backend with: cd backend && dotnet run"