using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

var builder = WebApplication.CreateBuilder(args);

// Configure database
if (File.Exists("appsettings.json"))
{
    builder.Configuration.AddJsonFile("appsettings.json", optional: false);
}

builder.Services.AddDbContext<PipelineDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrEmpty(connectionString))
    {
        throw new InvalidOperationException("Database connection string is not configured");
    }
    
    options.UseNpgsql(connectionString);
    Console.WriteLine("Using PostgreSQL database");
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddSignalR();
builder.Services.AddCors(options =>
    options.AddPolicy("VueApp", policy => 
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()));

var app = builder.Build();

// Initialize database - work with existing database
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<PipelineDbContext>();
    try
    {
        // Try to connect to the database first
        var canConnect = await dbContext.Database.CanConnectAsync();
        Console.WriteLine($"Database connection: {canConnect}");
        
        if (canConnect)
        {
            // Check if tables exist, if not create them
            try
            {
                var projectsCount = await dbContext.Projects.CountAsync();
                Console.WriteLine($"Existing projects count: {projectsCount}");
                Console.WriteLine("Database and tables already exist, skipping creation.");
            }
            catch (Exception)
            {
                Console.WriteLine("Tables don't exist, creating them...");
                await dbContext.Database.EnsureCreatedAsync();
            }
        }
        else
        {
            Console.WriteLine("Cannot connect to database, trying to create tables...");
            await dbContext.Database.EnsureCreatedAsync();
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Database initialization failed: {ex.Message}");
        Console.WriteLine("Trying alternative database setup...");
        
        // Fallback: try to create tables using raw SQL
        try
        {
            await SetupDatabaseWithRawSql(dbContext);
        }
        catch (Exception ex2)
        {
            Console.WriteLine($"Alternative setup also failed: {ex2.Message}");
            Console.WriteLine("Application will continue but database operations may fail.");
        }
    }
}

app.UseCors("VueApp");
app.UseRouting();
app.MapControllers();
app.MapHub<SimulationHub>("/simulationHub");

app.MapGet("/", () => "Pipeline Designer API is running!");
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow }));

app.MapGet("/db-health", async (PipelineDbContext dbContext) => 
{
    try
    {
        var canConnect = await dbContext.Database.CanConnectAsync();
        var projectCount = await dbContext.Projects.CountAsync();
        
        return Results.Ok(new 
        { 
            status = "Healthy", 
            database = canConnect ? "Connected" : "Disconnected",
            projectsCount = projectCount,
            timestamp = DateTime.UtcNow
        });
    }
    catch (Exception ex)
    {
        return Results.Ok(new 
        { 
            status = "Unhealthy", 
            error = ex.Message,
            timestamp = DateTime.UtcNow
        });
    }
});

// Fallback database setup using raw SQL
async Task SetupDatabaseWithRawSql(PipelineDbContext dbContext)
{
    var connection = dbContext.Database.GetDbConnection();
    await connection.OpenAsync();
    
    try
    {
        // Create tables manually
        using var command = connection.CreateCommand();
        
        // Create Projects table
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS ""Projects"" (
                ""Id"" text PRIMARY KEY,
                ""Name"" text NOT NULL,
                ""Type"" text NOT NULL,
                ""CreatedAt"" timestamp with time zone NOT NULL,
                ""UpdatedAt"" timestamp with time zone NOT NULL
            );";
        await command.ExecuteNonQueryAsync();
        
        // Create Nodes table
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS ""Nodes"" (
                ""Id"" text PRIMARY KEY,
                ""ProjectId"" text NOT NULL REFERENCES ""Projects""(""Id"") ON DELETE CASCADE,
                ""Type"" text NOT NULL,
                ""Position"" jsonb NOT NULL,
                ""Properties"" jsonb NOT NULL
            );";
        await command.ExecuteNonQueryAsync();
        
        // Create Connections table
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS ""Connections"" (
                ""Id"" text PRIMARY KEY,
                ""ProjectId"" text NOT NULL REFERENCES ""Projects""(""Id"") ON DELETE CASCADE,
                ""SourceId"" text NOT NULL,
                ""TargetId"" text NOT NULL,
                ""Properties"" jsonb NOT NULL
            );";
        await command.ExecuteNonQueryAsync();
        
        // Create indexes
        command.CommandText = @"
            CREATE INDEX IF NOT EXISTS ""IX_Nodes_ProjectId"" ON ""Nodes"" (""ProjectId"");
            CREATE INDEX IF NOT EXISTS ""IX_Connections_ProjectId"" ON ""Connections"" (""ProjectId"");";
        await command.ExecuteNonQueryAsync();
        
        Console.WriteLine("Database tables created successfully using raw SQL");
    }
    finally
    {
        await connection.CloseAsync();
    }
}

app.Run("http://localhost:5000");

public class SimulationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("Connected", "Welcome to Simulation Hub");
        await base.OnConnectedAsync();
    }

    public async Task StartSimulation(string projectId, object project)
    {
        await Clients.Caller.SendAsync("SimulationStarted", new { projectId });

        for (int i = 0; i <= 100; i += 10)
        {
            await Task.Delay(200);
            await Clients.Caller.SendAsync("SimulationProgress", i);
        }

        await Clients.Caller.SendAsync("SimulationCompleted", new
        {
            projectId,
            isSuccessful = true,
            nodeResults = new Dictionary<string, object>(),
            connectionResults = new Dictionary<string, object>()
        });
    }

    public async Task StopSimulation(string projectId)
    {
        await Clients.Caller.SendAsync("SimulationStopped", projectId);
    }
}

[ApiController]
[Route("api/[controller]")]
public class PipelineController : ControllerBase
{
    private readonly PipelineDbContext _context;

    public PipelineController(PipelineDbContext context)
    {
        _context = context;
    }

    [HttpGet("projects")]
    public async Task<IActionResult> GetProjects()
    {
        try
        {
            var projects = await _context.Projects
                .OrderByDescending(p => p.UpdatedAt)
                .Take(50)
                .Select(p => new
                {
                    id = p.Id,
                    name = p.Name,
                    type = p.Type,
                    createdAt = p.CreatedAt,
                    updatedAt = p.UpdatedAt
                })
                .ToListAsync();

            return Ok(projects);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting projects: {ex.Message}");
            return Ok(new List<object>());
        }
    }

    [HttpGet("projects/{id}")]
    public async Task<IActionResult> GetProject(string id)
    {
        try
        {
            var project = await _context.Projects
                .Include(p => p.Nodes)
                .Include(p => p.Connections)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
                return NotFound(new { error = "Project not found" });

            return Ok(project);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting project {id}: {ex.Message}");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpPost("projects")]
    public async Task<IActionResult> CreateProject([FromBody] PipelineProject project)
    {
        try
        {
            // Generate new ID if not provided
            if (string.IsNullOrEmpty(project.Id) || project.Id == "undefined")
            {
                project.Id = Guid.NewGuid().ToString();
            }

            project.CreatedAt = DateTime.UtcNow;
            project.UpdatedAt = DateTime.UtcNow;

            // Ensure nodes and connections have proper IDs and references
            if (project.Nodes != null)
            {
                foreach (var node in project.Nodes)
                {
                    if (string.IsNullOrEmpty(node.Id) || node.Id == "undefined")
                    {
                        node.Id = Guid.NewGuid().ToString();
                    }
                    node.ProjectId = project.Id;
                    
                    // Ensure position is properly set
                    if (node.Position == null)
                    {
                        node.Position = new NodePosition { X = 100, Y = 100 };
                    }
                    // Ensure Properties is not null
                    node.Properties ??= new Dictionary<string, object>();
                }
            }

            if (project.Connections != null)
            {
                foreach (var connection in project.Connections)
                {
                    if (string.IsNullOrEmpty(connection.Id) || connection.Id == "undefined")
                    {
                        connection.Id = Guid.NewGuid().ToString();
                    }
                    connection.ProjectId = project.Id;
                    // Ensure Properties is not null
                    connection.Properties ??= new Dictionary<string, object>();
                }
            }

            _context.Projects.Add(project);
            await _context.SaveChangesAsync();

            Console.WriteLine($"Project created successfully: {project.Id}, {project.Name}");
            return Ok(project);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating project: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return StatusCode(500, new { error = $"Failed to create project: {ex.Message}" });
        }
    }

    [HttpPut("projects/{id}")]
    public async Task<IActionResult> UpdateProject(string id, [FromBody] PipelineProject project)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            Console.WriteLine($"Updating project with ID: {id}");
            Console.WriteLine($"Project nodes count: {project.Nodes?.Count ?? 0}");
            Console.WriteLine($"Project connections count: {project.Connections?.Count ?? 0}");

            var existingProject = await _context.Projects
                .Include(p => p.Nodes)
                .Include(p => p.Connections)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (existingProject == null)
            {
                Console.WriteLine("Project not found, creating new one...");
                project.Id = id;
                project.CreatedAt = DateTime.UtcNow;
                project.UpdatedAt = DateTime.UtcNow;

                // Ensure all nodes and connections have proper IDs and references
                if (project.Nodes != null)
                {
                    foreach (var node in project.Nodes)
                    {
                        if (string.IsNullOrEmpty(node.Id) || node.Id == "undefined")
                        {
                            node.Id = Guid.NewGuid().ToString();
                        }
                        node.ProjectId = id; // Set ProjectId explicitly
                        
                        if (node.Position == null)
                        {
                            node.Position = new NodePosition { X = 100, Y = 100 };
                        }
                        node.Properties ??= new Dictionary<string, object>();
                    }
                }

                if (project.Connections != null)
                {
                    foreach (var connection in project.Connections)
                    {
                        if (string.IsNullOrEmpty(connection.Id) || connection.Id == "undefined")
                        {
                            connection.Id = Guid.NewGuid().ToString();
                        }
                        connection.ProjectId = id; // Set ProjectId explicitly
                        connection.Properties ??= new Dictionary<string, object>();
                    }
                }

                _context.Projects.Add(project);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                Console.WriteLine("New project created successfully.");
                return Ok(project);
            }

            Console.WriteLine("Existing project found, updating...");
            
            // Update existing project
            existingProject.Name = project.Name;
            existingProject.Type = project.Type;
            existingProject.UpdatedAt = DateTime.UtcNow;

            // Remove old nodes and connections
            _context.Nodes.RemoveRange(existingProject.Nodes);
            _context.Connections.RemoveRange(existingProject.Connections);

            await _context.SaveChangesAsync();

            // Add new nodes
            if (project.Nodes != null)
            {
                foreach (var node in project.Nodes)
                {
                    node.ProjectId = id; // Set ProjectId explicitly
                    if (string.IsNullOrEmpty(node.Id) || node.Id == "undefined")
                    {
                        node.Id = Guid.NewGuid().ToString();
                    }
                    
                    if (node.Position == null)
                    {
                        node.Position = new NodePosition { X = 100, Y = 100 };
                    }
                    
                    node.Properties ??= new Dictionary<string, object>();
                    _context.Nodes.Add(node);
                }
            }

            // Add new connections
            if (project.Connections != null)
            {
                foreach (var connection in project.Connections)
                {
                    connection.ProjectId = id; // Set ProjectId explicitly
                    if (string.IsNullOrEmpty(connection.Id) || connection.Id == "undefined")
                    {
                        connection.Id = Guid.NewGuid().ToString();
                    }
                    
                    connection.Properties ??= new Dictionary<string, object>();
                    _context.Connections.Add(connection);
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            Console.WriteLine("Project updated successfully.");
            
            // Load updated project for return
            var updatedProject = await _context.Projects
                .Include(p => p.Nodes)
                .Include(p => p.Connections)
                .FirstOrDefaultAsync(p => p.Id == id);
                
            return Ok(updatedProject);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Console.WriteLine($"Error updating project {id}: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
            }
            return StatusCode(500, new { error = $"Failed to update project: {ex.Message}" });
        }
    }

    [HttpDelete("projects/{id}")]
    public async Task<IActionResult> DeleteProject(string id)
    {
        try
        {
            var project = await _context.Projects
                .Include(p => p.Nodes)
                .Include(p => p.Connections)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
                return NotFound(new { error = "Project not found" });

            _context.Projects.Remove(project);
            await _context.SaveChangesAsync();

            Console.WriteLine($"Project deleted successfully: {id}");
            return Ok(new { message = "Project deleted successfully" });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting project {id}: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return StatusCode(500, new { error = $"Failed to delete project: {ex.Message}" });
        }
    }

    [HttpPost("validate")]
    public IActionResult ValidateProject([FromBody] object project) => Ok(new
    {
        isValid = true,
        errors = new string[0],
        warnings = new string[0]
    });

    [HttpPost("simulate")]
    public async Task<IActionResult> SimulateProject([FromBody] SimulationRequest request)
    {
        try
        {
            // Simple simulation logic - in real app this would be more complex
            await Task.Delay(1000); // Simulate processing
            
            return Ok(new
            {
                projectId = request.ProjectId,
                isSuccessful = true,
                nodeResults = new Dictionary<string, object>(),
                connectionResults = new Dictionary<string, object>(),
                simulationTime = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Simulation error: {ex.Message}");
            return StatusCode(500, new { error = "Simulation failed" });
        }
    }

    [HttpPost("projects/{id}/export")]
    public async Task<IActionResult> ExportProject(string id)
    {
        try
        {
            var project = await _context.Projects
                .Include(p => p.Nodes)
                .Include(p => p.Connections)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
                return NotFound();

            var exportData = new
            {
                project.Id,
                project.Name,
                project.Type,
                project.Nodes,
                project.Connections,
                project.CreatedAt,
                project.UpdatedAt
            };

            var fileName = $"{project.Name}_{DateTime.UtcNow:yyyyMMddHHmmss}.json";
            var fileBytes = JsonSerializer.SerializeToUtf8Bytes(exportData, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            return File(fileBytes, "application/json", fileName);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error exporting project {id}: {ex.Message}");
            return StatusCode(500, new { error = "Failed to export project" });
        }
    }

    [HttpPost("projects/import")]
    public async Task<IActionResult> ImportProject(IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            using var stream = new StreamReader(file.OpenReadStream());
            var content = await stream.ReadToEndAsync();

            var project = JsonSerializer.Deserialize<PipelineProject>(content, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            });

            if (project == null)
                return BadRequest("Invalid project file");

            // Generate new IDs to avoid conflicts
            project.Id = Guid.NewGuid().ToString();
            project.CreatedAt = DateTime.UtcNow;
            project.UpdatedAt = DateTime.UtcNow;

            if (project.Nodes != null)
            {
                foreach (var node in project.Nodes)
                {
                    node.Id = Guid.NewGuid().ToString();
                    node.ProjectId = project.Id;
                    node.Properties ??= new Dictionary<string, object>();
                }
            }

            if (project.Connections != null)
            {
                foreach (var connection in project.Connections)
                {
                    connection.Id = Guid.NewGuid().ToString();
                    connection.ProjectId = project.Id;
                    connection.Properties ??= new Dictionary<string, object>();
                }
            }

            _context.Projects.Add(project);
            await _context.SaveChangesAsync();

            return Ok(project);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error importing project: {ex.Message}");
            return StatusCode(500, new { error = "Failed to import project" });
        }
    }
}

public class SimulationRequest
{
    public string ProjectId { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
}

public class PipelineDbContext : DbContext
{
    public PipelineDbContext(DbContextOptions<PipelineDbContext> options) : base(options) { }

    public DbSet<PipelineProject> Projects { get; set; }
    public DbSet<PipelineNode> Nodes { get; set; }
    public DbSet<PipelineConnection> Connections { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
    modelBuilder.Entity<PipelineProject>()
        .HasKey(p => p.Id);

    modelBuilder.Entity<PipelineNode>()
        .HasKey(n => n.Id);

    modelBuilder.Entity<PipelineConnection>()
        .HasKey(c => c.Id);

    modelBuilder.Entity<PipelineProject>()
        .HasMany(p => p.Nodes)
        .WithOne()
        .HasForeignKey(n => n.ProjectId)
        .OnDelete(DeleteBehavior.Cascade);

    modelBuilder.Entity<PipelineProject>()
        .HasMany(p => p.Connections)
        .WithOne()
        .HasForeignKey(c => c.ProjectId)
        .OnDelete(DeleteBehavior.Cascade);

    // Improved JSON serialization configuration
    modelBuilder.Entity<PipelineNode>()
        .Property(n => n.Properties)
        .HasColumnType("jsonb")
        .HasConversion(
            v => System.Text.Json.JsonSerializer.Serialize(v, new System.Text.Json.JsonSerializerOptions 
            { 
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase 
            }),
            v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(v, new System.Text.Json.JsonSerializerOptions 
            { 
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase 
            }) ?? new Dictionary<string, object>()
        );

    modelBuilder.Entity<PipelineNode>()
        .Property(n => n.Position)
        .HasColumnType("jsonb")
        .HasConversion(
            v => System.Text.Json.JsonSerializer.Serialize(v, new System.Text.Json.JsonSerializerOptions 
            { 
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase 
            }),
            v => System.Text.Json.JsonSerializer.Deserialize<NodePosition>(v, new System.Text.Json.JsonSerializerOptions 
            { 
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase 
            }) ?? new NodePosition()
        );

    modelBuilder.Entity<PipelineConnection>()
        .Property(c => c.Properties)
        .HasColumnType("jsonb")
        .HasConversion(
            v => System.Text.Json.JsonSerializer.Serialize(v, new System.Text.Json.JsonSerializerOptions 
            { 
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase 
            }),
            v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(v, new System.Text.Json.JsonSerializerOptions 
            { 
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase 
            }) ?? new Dictionary<string, object>()
        );
   }
    
}

public class PipelineProject
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public string Name { get; set; } = "New Project";

    [Required]
    public string Type { get; set; } = "gas";

    public List<PipelineNode> Nodes { get; set; } = new();
    public List<PipelineConnection> Connections { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class PipelineNode
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string ProjectId { get; set; } = string.Empty;

    [Required]
    public string Type { get; set; } = string.Empty;

    public NodePosition Position { get; set; } = new();
    public Dictionary<string, object> Properties { get; set; } = new();
}

public class PipelineConnection
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string ProjectId { get; set; } = string.Empty;

    [Required]
    public string SourceId { get; set; } = string.Empty;

    [Required]
    public string TargetId { get; set; } = string.Empty;

    public Dictionary<string, object> Properties { get; set; } = new();
}

public class NodePosition
{
    public double X { get; set; }
    public double Y { get; set; }
}
