# AudioCopy - 投放你的音频到其他设备 Cast your audio to other devices


<a href="https://apps.microsoft.com/detail/9P3XT4FS327L?cid=github_readme&mode=direct">
	<img src="https://get.microsoft.com/images/en-us%20dark.svg" width="200"/>
</a>

下载AudioCopy

# 简介
你是不是为了把音频从一台设备投放到另一台设备上而发愁？有了AudioCopy，你不再需要为这种情况发愁了。

AudioCopy可以以几乎无损的音质、相对较低的延迟把一个设备上的音频投放到另一台设备上

# 功能列表
- [x] 投放音频
- [x] 同步媒体信息
- [x] 本地化
- [ ] 跨平台适配
- [ ] 把一个端点的音频复制到另一个端点
- [ ] 录制播放的音频


# AudioCopy 是如何工作的
AudioCopy的音频传输部分基于[AudioClone](https://github.com/0xeeeeeeeeeeee/AudioClone)构建

下面是一个简化的AduioClone的工作流程。
```mermaid
graph TD
    A1[Application 1] -- DirectSound--> DS[DirectSound API]
    A4[Application 2] -- DirectSound--> DS
    A2[System Sounds] -- DirectSound--> DS
    DS -- WASAPI shared mode--> B[WASAPI render client]
    A3[Application 3] -- WASAPI shared mode--> B
    B --> C[Audio engine]
    C -- Loopback source----> G[Loopback data offload]
    C ---> D[System Mixer]
    
    
    subgraph Capture Application
    G --> H[WasapiLoopbackCapture]
    H --> I[PCM Stream provider]
    end

    subgraph User mode
    A1
    A2
    A3
    A4
    DS
    B
    C
    D
    H
    I
    end


    subgraph Kernel mode
    D --> E[Audio driver stack]
    end


    subgraph Endpoint device
    E --> F[Hardware Decoder/DAC]


    end


    subgraph AudioClone
    I -- Register a stream--> S[Audio stream]
    S --> K["Codec<br>(MP3,WAV,FLAC)"]
    S -- RAW PCM data --> M
    K --> M["AudioClone.Server<br>(via ASP.NET Web server)"]

    M --> N[HTTP Endpoint]
    I -- Register a stream--> S1[Audio stream]


    S1 --> Z[AudioClone<br>Loopback recorder]
    Z --> Z2["Codec<br>(MP3,WAV,FLAC)"]
    

    I -- Register a stream--> S2[Audio stream]

    S2 --> V[AudioClone Repeater]
    V -->V1[AudioClone Repeater Player]
    
    
    end

    N --> M1[Another devices]
    Z2 ---> File
    V1 -- WASAPI Shared mode---> V2[Another endpoint device]
    F -- Analogue/Digital signal-------> X[Speaker, earphones, TV speaker...]


```

请注意`AudioClone`功能目前完全尚未实现，会在后续的版本中完善。


# 许可证

该项目遵循GNU GPLv2许可证 - 详细信息请参见[LICENSE](LICENSE)

