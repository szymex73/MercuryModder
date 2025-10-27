# Lib
Hosts any external code that the project depends on but can't be included as package references:
- [UAssetAPI](https://github.com/atenfyr/UAssetAPI/tree/8aa90add4da7bc17be87229044d02b3b94247ecf/UAssetAPI) - used for manipulating UE assets
- [SonicAudioLib](https://github.com/blueskythlikesclouds/SonicAudioTools/tree/178b025d7e17f5b781c678f738772871e0f1d30a) - used for manipulating CriWare files
- [VGAudio & VGAudio.Cli](https://github.com/LazyBone152/XV2-Tools/blob/ef73d0b413be405de40ce0172e7d5ad83c6100c6/LB_Common/Audio/) - used for converting WAV to HCA

Modifications made:
- SonicAudioLib
    - `SonicAudioLib/Archives/CriAfs2Archive.cs` - L162 was adjusted to be consistent with files in Mercury
    - `SonicAudioLib/CriMw/CriRowCollection.cs` - `Sort(Comparer<CriRow> comparer)` and `void Remove(int index)` were introduced
    - `SonicAudioLib/CriMw/CriTableReader.cs` - `shift-jis` encoding type was disabled
    - `SonicAudioLib/CriMw/CriTableWriter.cs` - `shift-jis` encoding type was disabled and switched to UTF8
- VGAudio
    - Adjusted usage of `Parallel.For` to `VGAudio.Utilities.Parallel.For` because of clashing namespaces
