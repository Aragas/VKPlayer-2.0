[VKPlayer]
Measure=Plugin
Plugin=VKPlayer.dll
PlayerType=CREDITS
SaveAudio=False

[State]
Measure=Plugin
Plugin=VKPlayer.dll
PlayerType=STATE
Substitute="0":"Player\MusicPlay.W.png","1":"Player\MusicPause.W.png","2":"Player\MusicPlay.W.png"

[Artist]
Measure=Plugin
Plugin=VKPlayer.dll
PlayerType=ARTIST

[Title]
Measure=Plugin
Plugin=VKPlayer.dll
PlayerType=TITLE

[Progress]
Measure=Plugin
Plugin=VKPlayer.dll
PlayerType=PROGRESS
MaxValue=1
MaxValue=0

[Repeat]
Measure=Plugin
Plugin=VKPlayer.dll
PlayerType=REPEAT
Substitute="0":"255","1":"128"

[Shuffle]
Measure=Plugin
Plugin=VKPlayer.dll
PlayerType=SHUFFLE
Substitute="0":"255","1":"128"

[MeterArtist]
Meter=STRING
MeasureName=Artist
X=10
Y=0
FontFace=Proxima
FontSize=10
FontColor=250,250,250,255
AntiAlias=1

[MeterTitle]
Meter=STRING
MeasureName=Title
X=10
Y=15
FontFace=Proxima
FontSize=10
FontColor=250,250,250,255
AntiAlias=1


[MeterPrev]
Meter=Image
MeterStyle=StyleButtons
ImageName=Player\MusicPrevious.W.png
LeftMouseUpAction=[!CommandMeasure "VKPlayer" "Previous"][!Update]
X=12

[MeterPlayPause]
Meter=Image
MeterStyle=StyleButtons
MeasureName=State
LeftMouseUpAction=[!CommandMeasure "VKPlayer" "PlayPause"][!Update]

[MeterNext]
Meter=Image
MeterStyle=StyleButtons
ImageName=Player\MusicNext.W.png
LeftMouseUpAction=[!CommandMeasure "VKPlayer" "Next"][!Update]

[MeterRepeat]
Meter=Image
MeterStyle=StyleButtons
ImageName=Player\MusicRepeat.W.png
ImageAlpha=[Repeat]
LeftMouseUpAction=[!CommandMeasure "VKPlayer" "SetRepeat -1"][!Update]
DynamicVariables=1

[MeterShuffle]
Meter=Image
MeterStyle=StyleButtons
ImageName=Player\MusicShuffle.W.png
ImageAlpha=[Shuffle]
LeftMouseUpAction=[!CommandMeasure "VKPlayer" "SetShuffle -1"][!Update]
DynamicVariables=1

[MeterVolumeUp]
Meter=Image
MeterStyle=StyleButtons
ImageName=Player\Volume.0.W.png
LeftMouseUpAction=[!CommandMeasure "VKPlayer" "SetVolume +10"][!Update]

[MeterVolumeDown]
Meter=Image
MeterStyle=StyleButtons
ImageName=Player\Volume.1.W.png
LeftMouseUpAction=[!CommandMeasure "VKPlayer" "SetPosition +5"][!Update]

[MetersaveToFile]
Meter=Image
MeterStyle=StyleButtons
ImageName=Player\Volume.1.W.png
LeftMouseUpAction=[!CommandMeasure "VKPlayer" "SetSaveToFile -1"][!Update]

[StyleButtons]
W=12
H=12
X=20r
Y=35

[MeterProgress]
MeasureName=Progress
Meter=BAR
Flip=0
BarColor=185,250,160,255
SolidColor=150,150,150,255
BarOrientation=Horizontal
X=12
Y=50
W=180
H=10