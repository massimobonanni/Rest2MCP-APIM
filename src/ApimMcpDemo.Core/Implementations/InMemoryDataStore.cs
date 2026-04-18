using ApimMcpDemo.Core.Entities;
using ApimMcpDemo.Core.Interfaces;

namespace ApimMcpDemo.Core.Implementations;

/// <summary>
/// Thread-safe in-memory data store seeded with demo data for products,
/// stock levels, IT support tickets, knowledge base articles, and restock orders.
/// Registered as a singleton so all function invocations share the same state.
/// </summary>
public class InMemoryDataStore : IDataStore
{
    private readonly object _lock = new();

    private readonly List<Product> _products;
    private readonly Dictionary<string, StockLevel> _stockLevels;
    private readonly List<Ticket> _tickets;
    private readonly List<KbArticle> _kbArticles;
    private readonly List<RestockOrderResult> _restockOrders;

    public InMemoryDataStore()
    {
        _products = SeedProducts();
        _stockLevels = SeedStockLevels();
        _tickets = SeedTickets();
        _kbArticles = SeedKbArticles();
        _restockOrders = new List<RestockOrderResult>();
    }

    // -------------------------------------------------------------------------
    // Products
    // -------------------------------------------------------------------------

    /// <summary>Returns a snapshot of all products, optionally filtered.</summary>
    public List<Product> SearchProducts(string? query, string? category)
    {
        lock (_lock)
        {
            IEnumerable<Product> results = _products;

            if (!string.IsNullOrWhiteSpace(category))
                results = results.Where(p =>
                    p.Category.Equals(category, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(query))
            {
                var q = query.ToLowerInvariant();
                results = results.Where(p =>
                    p.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    p.Description.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    p.Sku.Contains(q, StringComparison.OrdinalIgnoreCase));
            }

            return results.ToList();
        }
    }

    /// <summary>Returns a single product by ID, or null.</summary>
    public Product? GetProduct(string id)
    {
        lock (_lock)
        {
            return _products.FirstOrDefault(p =>
                p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>Returns the stock level for a product, or null.</summary>
    public StockLevel? GetStockLevel(string productId)
    {
        lock (_lock)
        {
            return _stockLevels.TryGetValue(productId, out var stock) ? stock : null;
        }
    }

    /// <summary>Adds a new restock order result to the store.</summary>
    public void AddRestockOrder(RestockOrderResult order)
    {
        lock (_lock)
        {
            _restockOrders.Add(order);
        }
    }

    /// <summary>Returns a snapshot of all restock orders.</summary>
    public List<RestockOrderResult> GetRestockOrders()
    {
        lock (_lock) { return _restockOrders.ToList(); }
    }

    // -------------------------------------------------------------------------
    // Tickets
    // -------------------------------------------------------------------------

    /// <summary>Returns tickets filtered by optional status, assignee, and priority.</summary>
    public List<Ticket> GetTicketsByStatus(string? status, string? assignee, string? priority = null)
    {
        lock (_lock)
        {
            IEnumerable<Ticket> results = _tickets;

            if (!string.IsNullOrWhiteSpace(status))
                results = results.Where(t =>
                    t.Status.Equals(status, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(assignee))
                results = results.Where(t =>
                    t.AssignedTo.Equals(assignee, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(priority))
                results = results.Where(t =>
                    t.Priority.Equals(priority, StringComparison.OrdinalIgnoreCase));

            return results.ToList();
        }
    }

    /// <summary>Returns a single ticket by ID, or null.</summary>
    public Ticket? GetTicket(string id)
    {
        lock (_lock)
        {
            return _tickets.FirstOrDefault(t =>
                t.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>Adds a new ticket to the store.</summary>
    public void AddTicket(Ticket ticket)
    {
        lock (_lock) { _tickets.Add(ticket); }
    }

    /// <summary>Updates an existing ticket in-place. Returns false if not found.</summary>
    public bool UpdateTicket(Ticket updated)
    {
        lock (_lock)
        {
            var idx = _tickets.FindIndex(t =>
                t.Id.Equals(updated.Id, StringComparison.OrdinalIgnoreCase));
            if (idx < 0) return false;
            _tickets[idx] = updated;
            return true;
        }
    }

    // -------------------------------------------------------------------------
    // Knowledge Base
    // -------------------------------------------------------------------------

    /// <summary>
    /// Searches KB articles using a case-insensitive contains match across
    /// Title, Summary, Content, and Tags. Optionally filters by category.
    /// </summary>
    public List<KbArticle> SearchKbArticles(string query, string? category = null)
    {
        lock (_lock)
        {
            var q = query.ToLowerInvariant();
            IEnumerable<KbArticle> results = _kbArticles.Where(a =>
                a.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                a.Summary.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                a.Content.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                a.Tags.Any(tag => tag.Contains(q, StringComparison.OrdinalIgnoreCase)));

            if (!string.IsNullOrWhiteSpace(category))
                results = results.Where(a =>
                    a.Category.Equals(category, StringComparison.OrdinalIgnoreCase));

            return results.ToList();
        }
    }

    /// <summary>Returns a single KB article by ID, or null.</summary>
    public KbArticle? GetKbArticle(string id)
    {
        lock (_lock)
        {
            return _kbArticles.FirstOrDefault(a =>
                a.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        }
    }

    // -------------------------------------------------------------------------
    // Seed data
    // -------------------------------------------------------------------------

    private static List<Product> SeedProducts() => new()
    {
        new Product { Id = "prod-001", Name = "Dell UltraSharp 27\" Monitor", Category = "Electronics",
            Description = "4K USB-C display with IPS panel and 99% sRGB colour accuracy.",
            Price = 649.99m, Sku = "DELL-U2723QE" },

        new Product { Id = "prod-002", Name = "Logitech MX Keys Keyboard", Category = "Electronics",
            Description = "Advanced wireless keyboard with smart illumination and multi-device pairing.",
            Price = 109.99m, Sku = "LOGI-MX-KEYS" },

        new Product { Id = "prod-003", Name = "Ergonomic Mesh Office Chair", Category = "Furniture",
            Description = "Breathable mesh back, adjustable lumbar support, and 4D armrests.",
            Price = 399.00m, Sku = "FURN-ERGO-CH1" },

        new Product { Id = "prod-004", Name = "Standing Desk Converter", Category = "Furniture",
            Description = "Sit-stand desktop converter with gas-spring mechanism, 80 cm wide.",
            Price = 249.00m, Sku = "FURN-STND-DC2" },

        new Product { Id = "prod-005", Name = "HP LaserJet Pro Toner Cartridge", Category = "Office Supplies",
            Description = "Black toner cartridge for HP LaserJet Pro M404 series, ~3000 pages yield.",
            Price = 54.99m, Sku = "HP-CF258A" },

        new Product { Id = "prod-006", Name = "A4 Copy Paper Ream (500 sheets)", Category = "Office Supplies",
            Description = "80 gsm white copy paper, suitable for laser and inkjet printers.",
            Price = 7.99m, Sku = "PAP-A4-80G" },

        new Product { Id = "prod-007", Name = "Jabra Evolve2 85 Headset", Category = "Electronics",
            Description = "Wireless ANC headset with professional-grade microphones, certified for UC.",
            Price = 349.00m, Sku = "JABRA-EV2-85" },

        new Product { Id = "prod-008", Name = "Whiteboard 120x90 cm", Category = "Furniture",
            Description = "Magnetic dry-erase whiteboard with aluminium frame and pen tray.",
            Price = 89.99m, Sku = "FURN-WB-120" }
    };

    private static Dictionary<string, StockLevel> SeedStockLevels()
    {
        var now = DateTime.UtcNow;
        var levels = new List<StockLevel>
        {
            new StockLevel { ProductId = "prod-001", Sku = "DELL-U2723QE",
                QuantityAvailable = 14, ReorderThreshold = 5,
                WarehouseLocation = "A-12-3", LastUpdated = now.AddDays(-1) },

            new StockLevel { ProductId = "prod-002", Sku = "LOGI-MX-KEYS",
                QuantityAvailable = 3, ReorderThreshold = 10,     // below threshold!
                WarehouseLocation = "B-04-1", LastUpdated = now.AddDays(-2) },

            new StockLevel { ProductId = "prod-003", Sku = "FURN-ERGO-CH1",
                QuantityAvailable = 8, ReorderThreshold = 3,
                WarehouseLocation = "C-01-2", LastUpdated = now.AddDays(-3) },

            new StockLevel { ProductId = "prod-004", Sku = "FURN-STND-DC2",
                QuantityAvailable = 2, ReorderThreshold = 4,      // below threshold!
                WarehouseLocation = "C-02-1", LastUpdated = now.AddDays(-1) },

            new StockLevel { ProductId = "prod-005", Sku = "HP-CF258A",
                QuantityAvailable = 4, ReorderThreshold = 15,     // well below threshold!
                WarehouseLocation = "D-06-3", LastUpdated = now.AddHours(-6) },

            new StockLevel { ProductId = "prod-006", Sku = "PAP-A4-80G",
                QuantityAvailable = 7, ReorderThreshold = 20,     // below threshold!
                WarehouseLocation = "D-07-1", LastUpdated = now.AddHours(-3) },

            new StockLevel { ProductId = "prod-007", Sku = "JABRA-EV2-85",
                QuantityAvailable = 11, ReorderThreshold = 5,
                WarehouseLocation = "B-09-2", LastUpdated = now.AddDays(-2) },

            new StockLevel { ProductId = "prod-008", Sku = "FURN-WB-120",
                QuantityAvailable = 6, ReorderThreshold = 2,
                WarehouseLocation = "C-03-4", LastUpdated = now.AddDays(-4) }
        };
        return levels.ToDictionary(s => s.ProductId);
    }

    private static List<Ticket> SeedTickets()
    {
        var now = DateTime.UtcNow;
        return new List<Ticket>
        {
            new Ticket
            {
                Id = "tkt-001",
                Title = "Cannot connect to VPN from home",
                Description = "Since this morning I'm unable to establish a VPN connection. Error code: 800.",
                Status = "open", Priority = "high", Category = "network",
                AssignedTo = "alice.smith@company.com", RequestedBy = "john.doe@company.com",
                CreatedAt = now.AddHours(-4)
            },
            new Ticket
            {
                Id = "tkt-002",
                Title = "Laptop running very slowly after Windows update",
                Description = "After the KB5034441 update my laptop takes 10+ minutes to boot and applications are unresponsive.",
                Status = "in-progress", Priority = "medium", Category = "software",
                AssignedTo = "bob.jones@company.com", RequestedBy = "sarah.lee@company.com",
                CreatedAt = now.AddDays(-1)
            },
            new Ticket
            {
                Id = "tkt-003",
                Title = "Office printer not printing in color",
                Description = "The printer in Room 204 only prints in black and white even when color is selected.",
                Status = "in-progress", Priority = "low", Category = "hardware",
                AssignedTo = "alice.smith@company.com", RequestedBy = "marco.rossi@company.com",
                CreatedAt = now.AddDays(-2)
            },
            new Ticket
            {
                Id = "tkt-004",
                Title = "Request access to SharePoint project site",
                Description = "I need read/write access to the SharePoint site 'ProjectAlpha' for onboarding.",
                Status = "resolved", Priority = "medium", Category = "access",
                AssignedTo = "carol.white@company.com", RequestedBy = "new.employee@company.com",
                CreatedAt = now.AddDays(-3), ResolvedAt = now.AddDays(-1)
            },
            new Ticket
            {
                Id = "tkt-005",
                Title = "Outlook keeps crashing on startup",
                Description = "Outlook crashes immediately after the splash screen. Safe mode also fails.",
                Status = "open", Priority = "critical", Category = "software",
                AssignedTo = "support-team@company.com", RequestedBy = "director.cfo@company.com",
                CreatedAt = now.AddHours(-1)
            },
            new Ticket
            {
                Id = "tkt-006",
                Title = "Second monitor not detected after desk move",
                Description = "After moving to a new desk my second monitor is not detected. Tried different cables.",
                Status = "resolved", Priority = "low", Category = "hardware",
                AssignedTo = "bob.jones@company.com", RequestedBy = "jan.baker@company.com",
                CreatedAt = now.AddDays(-5), ResolvedAt = now.AddDays(-4)
            }
        };
    }

    private static List<KbArticle> SeedKbArticles() => new()
    {
        new KbArticle
        {
            Id = "kb-001",
            Title = "How to Set Up and Troubleshoot VPN Access",
            Summary = "Step-by-step guide for configuring the corporate VPN client and resolving common connection errors.",
            Category = "network",
            Tags = new List<string> { "vpn", "remote access", "network", "cisco anyconnect", "error 800" },
            HelpfulVotes = 142,
            Content = @"## VPN Setup Guide

### Prerequisites
- Cisco AnyConnect client installed (download from IT Portal)
- Your corporate email and password
- Multi-factor authentication app configured

### Configuration Steps
1. Open Cisco AnyConnect
2. Enter the VPN gateway address: `vpn.company.com`
3. Click Connect and authenticate with your corporate credentials
4. Approve the MFA prompt on your authenticator app

### Common Errors
**Error 800 – Unable to establish VPN:**
- Ensure your internet connection is working
- Check that UDP port 443 is not blocked by your home router/firewall
- Try switching to 'SSL (TCP)' mode in AnyConnect preferences
- Restart the VPN client and try again

**Authentication Failed:**
- Confirm your password hasn't expired (check via https://myaccount.company.com)
- Ensure your MFA token is in sync

### Still not working?
Reset your VPN profile by deleting `%AppData%\Cisco\Cisco AnyConnect Secure Mobility Client\` and reconfiguring."
        },
        new KbArticle
        {
            Id = "kb-002",
            Title = "Password Reset and Account Unlock Guide",
            Summary = "How to reset your corporate password using self-service, and what to do if your account is locked.",
            Category = "access",
            Tags = new List<string> { "password", "reset", "account locked", "self-service", "SSPR" },
            HelpfulVotes = 312,
            Content = @"## Password Reset Guide

### Self-Service Password Reset (SSPR)
1. Go to https://passwordreset.microsoftonline.com
2. Enter your corporate email address
3. Choose your verification method (email, phone, or authenticator app)
4. Follow the prompts to set a new password

### Password Requirements
- Minimum 12 characters
- Must include uppercase, lowercase, number, and special character
- Cannot reuse last 10 passwords

### Account Locked Out?
Accounts are locked after 10 failed login attempts.
- Wait 30 minutes for automatic unlock, OR
- Contact IT Helpdesk at ext. 1234 for immediate unlock

### Changing Password Before Expiry
Passwords expire every 90 days. You will receive email reminders at 14, 7, and 1 day before expiry.
Change your password at: https://myaccount.company.com"
        },
        new KbArticle
        {
            Id = "kb-003",
            Title = "Printer Configuration and Troubleshooting",
            Summary = "How to add a network printer and resolve common printing issues including colour output problems.",
            Category = "hardware",
            Tags = new List<string> { "printer", "printing", "color", "colour", "driver", "network printer" },
            HelpfulVotes = 87,
            Content = @"## Printer Setup and Troubleshooting

### Adding a Network Printer (Windows 10/11)
1. Open Settings > Bluetooth & devices > Printers & scanners
2. Click 'Add device' and wait for discovery
3. If the printer is not found automatically, click 'The printer I want isn't listed'
4. Select 'Add a printer using a TCP/IP address or hostname'
5. Enter the printer IP (see label on printer or ask IT)

### Colour Printing Not Working
1. Open the print dialog and click 'Printer Properties'
2. Go to the 'Color' or 'Finishing' tab
3. Ensure 'Print in Grayscale' is NOT checked
4. Verify the printer has colour toner cartridges installed (ask facilities)
5. Download and reinstall the latest driver from the manufacturer website

### Paper Jams
1. Open all access doors and gently remove jammed paper
2. Do not pull paper against the feed direction
3. Check for torn paper fragments before closing doors

### Printer Offline
1. Right-click the printer in Settings and choose 'See what's printing'
2. Click Printer menu > uncheck 'Use Printer Offline'
3. Clear any stuck print jobs from the queue"
        },
        new KbArticle
        {
            Id = "kb-004",
            Title = "Improving Laptop Performance and Slow Boot Times",
            Summary = "Steps to diagnose and resolve slow laptop performance, especially after Windows updates.",
            Category = "software",
            Tags = new List<string> { "laptop", "performance", "slow", "boot", "windows update", "optimization" },
            HelpfulVotes = 203,
            Content = @"## Laptop Performance Troubleshooting

### Quick Fixes
1. **Restart your laptop** – a full restart clears memory and finishes pending updates
2. **Check Windows Update** – ensure all updates are installed (Settings > Windows Update)
3. **Free up disk space** – run Disk Cleanup (search in Start menu); ensure at least 15% disk is free

### Diagnosing Slow Performance After Update
1. Open Task Manager (Ctrl+Shift+Esc) and check CPU/Memory/Disk usage
2. If 'Windows Modules Installer Worker' or 'antimalware service' is high, wait 1-2 hours for background tasks to complete
3. If 'SysMain' (Superfetch) is causing high disk usage, it can be disabled via Services.msc

### Performance Optimisation Checklist
- Disable startup programs: Task Manager > Startup tab
- Run `sfc /scannow` in Admin command prompt to fix corrupted files
- Check for malware using Windows Defender full scan
- Update device drivers, especially display and storage drivers

### When to Escalate
If the laptop still performs poorly after these steps, opening a support ticket for hardware assessment is recommended. Include the Task Manager screenshot and Event Viewer errors."
        },
        new KbArticle
        {
            Id = "kb-005",
            Title = "Software Installation Requests and Approved Applications",
            Summary = "How to request software installation and the list of pre-approved applications available via the IT portal.",
            Category = "software",
            Tags = new List<string> { "software", "installation", "approved apps", "request", "self-service", "portal" },
            HelpfulVotes = 156,
            Content = @"## Software Installation Guide

### Self-Service: Pre-Approved Applications
The following software can be installed without approval via the Company Software Portal (https://software.company.com):
- Microsoft Office 365 apps (Word, Excel, PowerPoint, Teams)
- Adobe Acrobat Reader
- Google Chrome / Firefox
- Zoom / Webex
- 7-Zip, Notepad++, VS Code
- Cisco AnyConnect VPN Client

### Installation via Company Software Portal
1. Open the Software Portal from your browser or Start menu shortcut
2. Search for the application
3. Click 'Install' – the software will be deployed to your machine within 15 minutes
4. No admin rights required for portal installations

### Requesting Unlisted Software
For software not in the portal:
1. Submit a Software Request via the IT Helpdesk ticket system
2. Provide: software name, version, vendor, business justification
3. Allow 3-5 business days for security review and approval
4. Licence approval may be required if it is commercial software

### Installing Software Yourself (Admin Rights)
Standard users do not have local admin rights. If a business process requires admin access, request a 'Temporary Admin Rights' ticket providing a time-limited justification."
        }
    };
}
