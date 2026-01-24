package model

// DistroConfig represents a family of distributions (e.g., "Ubuntu")
type DistroConfig struct {
	Name     string             `json:"Name"`
	Versions map[string]Version `json:"Versions"`
}

// Version represents a specific version of a distro
type Version struct {
	Name        string `json:"Name"`
	Url         string `json:"Url"`
	DefaultName string `json:"DefaultName"`
	Filename    string `json:"Filename"`
	Source      string `json:"Source,omitempty"`
	LocalPath   string `json:"LocalPath,omitempty"`
}

// GlobalSettings represents the application settings
type GlobalSettings struct {
	DefaultInstallPath string `json:"DefaultInstallPath"`
	DefaultDistro      string `json:"DefaultDistro"`
	DistroCachePath    string `json:"DistroCachePath"`
	DistroSourceUrl    string `json:"DistroSourceUrl,omitempty"`
	// DefaultTerminalStartPath acts as the starting directory when opening a terminal.
	// If empty, it defaults to the user's home directory inside the distro ("~").
	DefaultTerminalStartPath string          `json:"DefaultTerminalStartPath,omitempty"`
	CustomPackages           []CustomPackage `json:"CustomPackages"`
}

// CustomPackage represents a user-defined source
type CustomPackage struct {
	Name      string `json:"Name"`
	Version   string `json:"Version"`
	PathOrUrl string `json:"PathOrUrl"`
}
