 # Raiden Project

This project provides a user interface application that captures screen regions, recognizes images, and displays recognized numbers with high precision and speed.

## Features

- High-resolution screen capture.
- Real-time image recognition with OpenCvSharp.
- Animated UI feedback based on recognition results.
- Keyboard hook integration for responsive control.
- Customizable settings through an INI configuration file.

## Requirements

- Windows OS with .NET Framework.
- Screen resolution set to 3840x2160 for optimal performance. I might provide other resolution support in future.
- OpenCvSharp library for image processing tasks.

## Configuration

To customize the application settings, modify the `config.ini` file with your preferred key bindings and parameters.

Default key settings:
- `elementSkillKey`: The key to activate the element skill (Default: "E").
- `HideShowKey`: The key to toggle the visibility of the application window (Default: "F11").

## Usage

0. Download the two zip files and extract to a directory ,modify key if you like .
1. Start the application. It will automatically hook into the keyboard events.
2. Press the designated `elementSkillKey` to activate the element skill.
3. The UI will animate the background of text blocks to indicate the cooldown status.

## Development

This application is written in C# and utilizes several Windows Presentation Foundation (WPF) elements for the UI, such as `TextBlock`, `Border`, and `Window`. The screen capture and image recognition are handled by OpenCvSharp, which requires an understanding of image processing techniques.

## Contributing

Contributions to the Raiden project are welcome. Please feel free to fork the repository, make changes, and submit pull requests.

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Acknowledgments

- Thanks to the OpenCvSharp contributors for providing a powerful toolset for image processing.
- Gratitude to the .NET community for the continuous support and resources.

## Contact

For any queries or feedback, please create an issue in the project's GitHub repository.


