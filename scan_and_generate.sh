#!/bin/bash

# Script: generate_project_script_complete_fixed.sh
# Description: Scans current directory structure and creates a complete .sh script for project generation

set -e

echo "Scanning current project structure and generating complete setup script..."

OUTPUT_SCRIPT="complete_project_setup_fixed.sh"

# Function to check if file is binary
is_binary_file() {
    local file="$1"
    if [[ "$file" == *".png" ]] || [[ "$file" == *".jpg" ]] || [[ "$file" == *".jpeg" ]] || \
       [[ "$file" == *".gif" ]] || [[ "$file" == *".ico" ]] || [[ "$file" == *".svg" ]] || \
       [[ "$file" == *".woff" ]] || [[ "$file" == *".woff2" ]] || [[ "$file" == *".ttf" ]] || \
       [[ "$file" == *".eot" ]] || [[ "$file" == *".pdf" ]] || [[ "$file" == *".zip" ]] || \
       [[ "$file" == *".tar" ]] || [[ "$file" == *".gz" ]]; then
        return 0
    fi
    
    if file "$file" | grep -q -E '(binary|executable|image|font|archive)'; then
        return 0
    fi
    
    return 1
}

# Create initial script
cat > "$OUTPUT_SCRIPT" << 'SCRIPT_EOF'
#!/bin/bash

set -e

echo "=== Project Setup Script ==="
echo "This script will recreate the project structure in the current directory"

SCRIPT_EOF

# Function to add all files
add_all_files() {
    local dir="$1"
    local exclude_pattern="${2:-}"
    
    find "$dir" -type f | while read -r file; do
        # Skip hidden files and unwanted directories
        if [[ "$file" == *"/."* ]] || \
           [[ "$file" == "./$OUTPUT_SCRIPT" ]] || \
           [[ "$file" == "./generate_project_script"* ]] || \
           [[ "$file" == *".git/"* ]] || \
           [[ "$file" == *"node_modules/"* ]] || \
           [[ "$file" == *"bin/"* ]] || \
           [[ "$file" == *"obj/"* ]] || \
           [[ "$file" == *"dist/"* ]] || \
           [[ "$file" == *".DS_Store" ]]; then
            continue
        fi
        
        if [[ -n "$exclude_pattern" && "$file" =~ $exclude_pattern ]]; then
            continue
        fi
        
        local relative_path="${file#./}"
        local dir_path=$(dirname "$relative_path")
        local file_name=$(basename "$relative_path")
        
        echo "Adding file: $relative_path"
        
        # For binary files, use base64 encoding
        if is_binary_file "$file"; then
            cat >> "$OUTPUT_SCRIPT" << FILE_EOF

# ==================== $relative_path (binary) ====================

echo "Creating binary file: $relative_path..."
mkdir -p "$dir_path"
cat > "$relative_path.base64" << 'BASE64_EOF'
$(base64 "$file")
BASE64_EOF
base64 -d "$relative_path.base64" > "$relative_path" 2>/dev/null || echo "Warning: Could not decode base64 file properly"
rm -f "$relative_path.base64"

FILE_EOF
        else
            if [ ! -s "$file" ]; then
                cat >> "$OUTPUT_SCRIPT" << FILE_EOF

# ==================== $relative_path (empty) ====================

echo "Creating empty file: $relative_path..."
mkdir -p "$dir_path"
touch "$relative_path"

FILE_EOF
            else
                cat >> "$OUTPUT_SCRIPT" << FILE_EOF

# ==================== $relative_path ====================

echo "Creating $relative_path..."
mkdir -p "$dir_path"
cat > "$relative_path" << 'FILE_CONTENT_EOF'
$(cat "$file")
FILE_CONTENT_EOF

FILE_EOF
            fi
        fi
        
        # Set executable permissions for .sh files
        if [[ "$relative_path" == *".sh" ]]; then
            cat >> "$OUTPUT_SCRIPT" << EOF
chmod +x "$relative_path"
EOF
        fi
    done
}

# Call the function to add all files
add_all_files "."

echo "Complete setup script generated: $OUTPUT_SCRIPT"
echo "Don't forget to make it executable: chmod +x $OUTPUT_SCRIPT"