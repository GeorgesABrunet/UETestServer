# UETestServer
Flyweight Test Server for projects that use MQTT / TCP protocols

The purpose of this repo is to provide a template server for MQTT/TCP messaging with a GUI that enables the user to determine parameters for communication over messages in an additive manner.

BACKGROUND:

Have you ever had a project where you need to connect to a single/multiple clients using MQTT/TCP messaging yet deploying the server is a tedious task that takes too long and doesn't allow quick iteration?
This repo might be a solution for you, then. Originally built for Unreal Engine projects that require connecting to a server and communicating using MQTT & TCP messaging, this template allows you to emulate the server you'd be using to iterate on communication requirements and pretty much whatever else you can think of.

USAGE:

If you need this tool, I'm assuming you are already familiar with MQTT/TCP messaging and I'll just explain which buttons to press to make the car go vrrr.
Press start from the solution to build and deploy, then press the start button on the GUI only after you have defined the connection parameters of the server to your needs.

TCP: Define IP, port, etc. as needed.
MQTT: Define IP, port, host, User, Pass, etc. as needed. The server will create a client with the Client ID as its name that you will publish your messages through. MainWindow GUI parameters and MQTT message handling do not run on the same thread, so if you wish to name your server something else than "UETestServer" and not get spammed by message received notifications in the log, you have to hardcode the desired server name in the M_MqqtServer_MessageReceived() function, otherwise write support for it.

The messages (that don't contain audio!) are serialized to JSON before broadcast and so are built in corresponding structures that the user (that's you!) can define in the model.cs class. Audio-containing message support has been added to show how you would add parallel messaging for items that do not all fit into JSON, or would best translate as byte arrays for messaging (MQTT support only, TCP behavior is undefined).
For whatever parameters you add or remove from the FMessageTemplate structure and relevant substructures, you must account for its new parameters by adding the GUI field in the MainWindow.xaml file and link corresponding interactive behavior in the MainWindow.xaml.cs file if you wish to use modular messaging. You must then also include the new parameters to the build sections of modular messaging within MainWindow.xaml.cs. 

SEND MESSAGE TAB:
When using the send message tab, the server must be started to be able to hit the send message button. Other important things of note are the Topic area. This is where the user can write the topic to publish messages under, and must be included. An "event" field has been added as a shorthand for a potential subtopic that is dependent on project needs, but the behavior is there if need be. This is also the place to add new parameters to the GUI that the user wants to define in modular messages. If sending Audio/files that you wish to send as byte array payloads rather than JSON, you must define the appropriate parallel behavior for Topic handling. Add Modular MSG to Sequencer button will add the parameters of the current modular message, json & audio filenames necessary(if checkboxes to use those are checked True) to load up based on configuration in the Message Sequencer Tab.

MESSAGE SEQUENCER TAB:
Add the JSON file, Audio file, or both with the corresponding MQTT Topic to sequence by filling out their textbox names and then press Add Msg to Queue button. If you do not have a JSON file, etc, you can build it modularly in the Send Message Tab and hit that "Add Modular MSG to Sequencer" button. JSON files linked in the textbox will be fetched as JSONs, then converted to string when the Add Msg to Queue button is pressed.

That's about it, I suggest poking around before using so you can scale its functionality accordingly.