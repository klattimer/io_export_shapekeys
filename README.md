# Blender to Unity Shape Key Exporter

## Features

* Export/Import shape key shapes for use in Unity 5
* Export/Import shape key animations for use in Unity 5
* Export multiple-objects at a time
* Export Animation ranges for specific named animations

## Details

This project was re-written using MetaMorph and io export diffmap as a baseline. Both of those projects were unsuitable for modern Blender and Unity so I redesigned the concept to work with more upto date releases. Demo project included provides a couple of good examples and a decent stress test scene.

## Usage

Add the plugin to blender, create animated shape keys, and name them in the "Shape Key Animation Ranges" panel, export using the export menu.

In unity, add a "ShapeKeysImport" component to an object, populate the references for this object and play the animation using ShapeKeyAnimations/ShapeKeyAnimations component. The shape keys and animations will be auto loaded from the JSON file.

## Requirements

* JSONObject https://github.com/mtschoen/JSONObject

## Future work

Still some work to be done, with the setup of the data files, testing shape keys in the GUI and validating settings before starting.
