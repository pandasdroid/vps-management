namespace VPSManager.Models;

public class Server
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string Username { get; set; } = "root";
    public string Password { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;
    public string Passphrase { get; set; } = string.Empty;
    public bool UsePrivateKey { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public string DisplayName => string.IsNullOrEmpty(Name) ? Host : Name;
}

public class ServerStats
{
    public string Hostname { get; set; } = "N/A";
    public string Os { get; set; } = "N/A";
    public string Kernel { get; set; } = "N/A";
    public string Uptime { get; set; } = "N/A";
    public string IpAddress { get; set; } = "N/A";
    public int CpuCores { get; set; } = 0;
    public double CpuPercent { get; set; } = 0;
    public long MemoryUsed { get; set; } = 0;
    public long MemoryTotal { get; set; } = 0;
    public double MemoryPercent { get; set; } = 0;
    public long DiskUsed { get; set; } = 0;
    public long DiskTotal { get; set; } = 0;
    public double DiskPercent { get; set; } = 0;
}

public enum ConnectionStatus
{
    Disconnected,
    Connecting,
    Connected,
    Error
}

public class FileItem
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public bool IsSymlink { get; set; }
    public long Size { get; set; }
    public string Permissions { get; set; } = string.Empty;
    public string ModifiedDate { get; set; } = string.Empty;

    public string Icon => GetIcon().icon;
    public string IconColor => GetIcon().color;
    public string IconFont => GetIcon().font;
    public string SizeDisplay => IsDirectory ? "" : FormatSize(Size);

    private (string icon, string color, string font) GetIcon()
    {
        const string segoe = "Segoe MDL2 Assets";
        const string devicon = "devicon";

        if (Name == "..") return ("\uE72B", "#8E8EA0", segoe);
        if (IsDirectory) return ("\uE8B7", "#F59E0B", segoe);
        if (IsSymlink) return ("\uE71B", "#8E8EA0", segoe);

        var ext = System.IO.Path.GetExtension(Name).ToLower();
        var nameLower = Name.ToLower();

        // Special filenames with Devicon
        if (nameLower is "dockerfile" or ".dockerignore") return ("\uE7B0", "#2496ED", devicon); // Docker
        if (nameLower.StartsWith(".git")) return ("\uE702", "#F05032", devicon); // Git
        if (nameLower is "package.json" or "package-lock.json") return ("\uE71E", "#CB3837", devicon); // npm
        if (nameLower is "yarn.lock") return ("\uE905", "#2C8EBB", devicon); // Yarn
        if (nameLower is "makefile" or "cmakelists.txt") return ("\uE70F", "#6D8086", segoe);
        if (nameLower.StartsWith(".env")) return ("\uE8D7", "#ECD53F", segoe);
        if (nameLower.Contains("license")) return ("\uE8A7", "#D4AA00", segoe);
        if (nameLower.Contains("readme")) return ("\uE8A5", "#ECECF1", segoe);

        return ext switch
        {
            // Programming Languages (Devicon with brand colors)
            ".py" or ".pyw" or ".pyi" => ("\uE73C", "#3776AB", devicon), // Python
            ".ipynb" => ("\uE73C", "#F37626", devicon), // Jupyter (Python icon, orange)
            ".js" or ".mjs" => ("\uE781", "#F7DF1E", devicon), // JavaScript
            ".jsx" => ("\uE7BA", "#61DAFB", devicon), // React
            ".ts" => ("\uE8CA", "#3178C6", devicon), // TypeScript
            ".tsx" => ("\uE7BA", "#61DAFB", devicon), // React (TSX)
            ".java" => ("\uE738", "#ED8B00", devicon), // Java
            ".kt" or ".kts" => ("\uE79B", "#7F52FF", devicon), // Kotlin
            ".scala" => ("\uE737", "#DC322F", devicon), // Scala
            ".groovy" => ("\uE775", "#4298B8", devicon), // Groovy
            ".c" => ("\uE649", "#A8B9CC", devicon), // C
            ".cpp" or ".cc" or ".cxx" => ("\uE646", "#00599C", devicon), // C++
            ".h" or ".hpp" or ".hxx" => ("\uE646", "#A8B9CC", devicon), // Header
            ".cs" => ("\uE648", "#512BD4", devicon), // C#
            ".fs" or ".fsx" => ("\uE7A8", "#378BBA", devicon), // F# (using rust icon as placeholder)
            ".vb" => ("\uE648", "#512BD4", devicon), // VB.NET
            ".go" => ("\uE724", "#00ADD8", devicon), // Go
            ".rs" => ("\uE7A8", "#DEA584", devicon), // Rust
            ".swift" => ("\uE755", "#F05138", devicon), // Swift
            ".dart" => ("\uE798", "#0175C2", devicon), // Dart
            ".rb" => ("\uE739", "#CC342D", devicon), // Ruby
            ".php" => ("\uE73D", "#777BB4", devicon), // PHP
            ".pl" or ".pm" => ("\uE769", "#39457E", devicon), // Perl
            ".lua" => ("\uE6B1", "#000080", devicon), // Lua
            ".r" => ("\uE6B3", "#276DC3", devicon), // R
            ".jl" => ("\uE624", "#9558B2", devicon), // Julia
            ".ex" or ".exs" => ("\uE6B7", "#6E4A7E", devicon), // Elixir
            ".erl" or ".hrl" => ("\uE7B1", "#A90533", devicon), // Erlang
            ".hs" or ".lhs" => ("\uE777", "#5D4F85", devicon), // Haskell
            ".clj" or ".cljs" => ("\uE76A", "#5881D8", devicon), // Clojure
            ".lisp" or ".cl" => ("\uE76A", "#3FB68B", devicon), // Lisp
            ".elm" => ("\uE62C", "#60B5CC", devicon), // Elm
            ".nim" => ("\uE6B8", "#FFE953", devicon), // Nim
            ".zig" => ("\uE6B4", "#F7A41D", devicon), // Zig (placeholder)
            ".sol" => ("\uE735", "#363636", devicon), // Solidity

            // Web Technologies (Devicon)
            ".html" or ".htm" => ("\uE736", "#E34F26", devicon), // HTML5
            ".css" => ("\uE749", "#1572B6", devicon), // CSS3
            ".scss" or ".sass" => ("\uE74B", "#CC6699", devicon), // Sass
            ".less" => ("\uE758", "#1D365D", devicon), // Less
            ".vue" => ("\uE906", "#4FC08D", devicon), // Vue.js
            ".svelte" => ("\uE904", "#FF3E00", devicon), // Svelte
            ".astro" => ("\uE903", "#FF5D01", devicon), // Astro (placeholder)
            ".angular" => ("\uE753", "#DD0031", devicon), // Angular

            // Shell/Terminal (Segoe MDL2)
            ".sh" or ".bash" or ".zsh" or ".fish" => ("\uE756", "#4EAA25", segoe),
            ".bat" or ".cmd" => ("\uE756", "#C1F12E", segoe),
            ".ps1" => ("\uE756", "#5391FE", segoe),

            // Config/Data (Devicon where available)
            ".json" => ("\uE60B", "#CBCB41", devicon), // JSON
            ".yaml" or ".yml" => ("\uE6B5", "#CB171E", devicon), // YAML (placeholder)
            ".xml" => ("\uE619", "#E34C26", devicon), // XML (placeholder)
            ".toml" => ("\uE713", "#9C4121", segoe),
            ".ini" or ".cfg" or ".conf" => ("\uE713", "#6D8086", segoe),
            ".sql" => ("\uE706", "#4479A1", devicon), // MySQL icon
            ".db" or ".sqlite" => ("\uE706", "#003B57", devicon),
            ".graphql" or ".gql" => ("\uE662", "#E10098", devicon), // GraphQL
            ".md" or ".markdown" => ("\uE73E", "#083FA1", devicon), // Markdown
            ".rst" => ("\uE8A5", "#ECECF1", segoe),

            // Documents (Segoe MDL2)
            ".txt" or ".text" => ("\uE8A5", "#ECECF1", segoe),
            ".pdf" => ("\uE8A5", "#FF0000", segoe),
            ".doc" or ".docx" => ("\uE8A5", "#2B579A", segoe),
            ".xls" or ".xlsx" or ".csv" => ("\uE80A", "#217346", segoe),
            ".ppt" or ".pptx" => ("\uE8A5", "#D24726", segoe),

            // Images (Segoe MDL2)
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".ico" => ("\uEB9F", "#26A69A", segoe),
            ".svg" => ("\uEB9F", "#FFB13B", segoe),
            ".psd" => ("\uE8B3", "#31A8FF", segoe),
            ".ai" => ("\uE8B3", "#FF9A00", segoe),
            ".fig" => ("\uE8B3", "#F24E1E", segoe),

            // Audio/Video (Segoe MDL2)
            ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" or ".m4a" => ("\uE8D6", "#FF5722", segoe),
            ".mp4" or ".avi" or ".mkv" or ".mov" or ".webm" or ".wmv" => ("\uE714", "#9C27B0", segoe),

            // Archives (Segoe MDL2)
            ".zip" or ".tar" or ".gz" or ".7z" or ".rar" or ".bz2" or ".xz" => ("\uE8B7", "#FFC107", segoe),
            ".deb" => ("\uE77F", "#A81D33", devicon), // Debian
            ".rpm" => ("\uE7BB", "#EE0000", devicon), // Red Hat

            // Project files (Devicon where available)
            ".sln" or ".csproj" or ".fsproj" => ("\uE70C", "#512BD4", devicon), // .NET
            ".gradle" => ("\uE660", "#02303A", devicon), // Gradle
            ".pom" => ("\uE738", "#C71A36", devicon), // Maven (Java icon)
            ".gemspec" => ("\uE739", "#CC342D", devicon), // Ruby

            // Executables (Segoe MDL2)
            ".exe" or ".app" or ".bin" or ".out" => ("\uE7AC", "#ECECF1", segoe),
            ".dll" or ".so" or ".dylib" => ("\uE74C", "#ECECF1", segoe),
            ".jar" or ".war" => ("\uE738", "#ED8B00", devicon), // Java
            ".class" => ("\uE738", "#ED8B00", devicon),

            // Security (Segoe MDL2)
            ".pem" or ".crt" or ".cer" or ".key" or ".pub" => ("\uE8D7", "#4CAF50", segoe),
            ".env" => ("\uE8D7", "#ECD53F", segoe),

            // Logs/Other (Segoe MDL2)
            ".log" => ("\uE7BA", "#8E8EA0", segoe),
            ".lock" => ("\uE72E", "#8E8EA0", segoe),
            ".diff" or ".patch" => ("\uE8E5", "#41B883", segoe),
            ".bak" or ".tmp" or ".swp" => ("\uE7BA", "#8E8EA0", segoe),

            // Fonts
            ".ttf" or ".otf" or ".woff" or ".woff2" => ("\uE8D2", "#ECECF1", segoe),

            _ => ("\uE7C3", "#ECECF1", segoe) // Generic file
        };
    }

    private static string FormatSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
}
