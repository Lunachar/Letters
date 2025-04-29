# Letters - Educational Game for Children with Cerebral Palsy

An interactive 2D game designed to help children with cerebral palsy learn letters through visual and auditory feedback.

## Features

- Responds to keyboard input with visual and audio feedback
- Shows letter images and related objects/animals
- Smooth parallax scrolling background
- Statistics tracking for letter usage
- Accessible UI with large, clear visuals
- Configurable auto-pronounce feature

## Requirements

- Unity 2021.3 or newer
- DOTween (Free version) from Asset Store
- New Input System package

## Setup Instructions

1. Clone this repository
2. Open the project in Unity
3. Import DOTween from the Asset Store
4. Install the Input System package via Package Manager if not already installed

## Creating Content

### Adding New Letters

1. In the Project window, right-click → Create → Letters → Letter Data
2. Fill in the following fields:
   - Letter: The character this data represents
   - Letter Name: Full name/pronunciation of the letter
   - Letter Sprite: Visual representation of the letter
   - Object Sprite: Image of an object/animal starting with this letter
   - Letter Sound: Audio clip of letter pronunciation
   - Object Sound: Audio clip of object/animal name or sound

### Content Database

1. Create a Content Database asset: right-click → Create → Letters → Content Database
2. Add all your Letter Data assets to the database
3. Assign the database to the GameManager in the scene

## Scene Setup

1. Create a new scene or open the existing one
2. Add the following prefabs to your scene:
   - GameManager
   - InputManager
   - LetterDisplayManager
   - EnvironmentScroller
3. Configure the references in the GameManager inspector
4. Set up your UI elements and connect them to the LetterDisplayManager

## Building the Game

1. File → Build Settings
2. Add your scene to the build
3. Select Windows as the target platform
4. Click Build
5. Choose your output location

## Usage

- Press any letter key (A-Z) to trigger the corresponding letter content
- All other keys will trigger background movement only
- Use the settings menu to toggle auto-pronounce feature

## Project Structure

```
Assets/
├── Scripts/
│   ├── Core/
│   │   └── GameManager.cs
│   ├── Data/
│   │   ├── LetterData.cs
│   │   └── ContentDatabase.cs
│   ├── Input/
│   │   └── InputManager.cs
│   ├── Environment/
│   │   └── EnvironmentScroller.cs
│   └── UI/
│       └── LetterDisplayManager.cs
├── Prefabs/
├── Scenes/
├── Sprites/
└── Audio/
``` 