# Outlines
A custom renderer feature for screen space outlines based on [Erik Roystan Ross Outline Shader](https://roystan.net/articles/outline-shader.html).

for more specific implementation details see [YouTube video](https://youtu.be/LMqio9NsqmM). Apart from minor adjustments no further improvements has been added.

## Usage
- Add files to your project
- Enable depth texture in the Universal Render Pipeline Asset
- Add the 'Screen Space Outlines' renderer feature to the Universal Renderer Data
- Adjust 'Screen Space Outlines' settings
- Go to Project Settings > Graphics and add the 3 new ones (Outlines, UnlitColor, and ViewSpaceNormals) to the list of Always Included Shaders. This is needed to make sure the shaders are included when building the project for a real device.

## Known Issues
- Does not work with objects that utilizes displacement shaders, the normal texture generation does not take the displacement into account
- Does not work with MSAA, the current work around is to use FXAA or SMAA on the camera instead.

## Future Plans
I'm continuously expanding on this implementation, below are some things planned for future releases:
- Outline generation anti aliasing
- Varying thickness of outlines based on distance from camera
- Fog affected outlines
- and much more
