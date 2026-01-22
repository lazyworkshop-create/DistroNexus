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
}

// GlobalSettings represents the application settings
type GlobalSettings struct {
	DefaultInstallPath string `json:"DefaultInstallPath"`
	DefaultDistro      string `json:"DefaultDistro"`
	DistroCachePath    string `json:"DistroCachePath"`
}
