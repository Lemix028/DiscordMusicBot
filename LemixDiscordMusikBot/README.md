- token --> Discord Bot API Token
- prefix --> obsolete
- StatusItems --> All status items
    - {0} --> Placeholder for guild count
- StatusRefreshTimer --> Refresh time in milliseconds (60000 is 1 minute)
- NoSongPicture --> URL of a picture
- DefaultVolume --> default volume (0-1000)


        StatusTypes:
        //
        // Zusammenfassung:
        //     User is offline.
        Offline = offline,
        //
        // Zusammenfassung:
        //     User is online.
        Online = online,
        //
        // Zusammenfassung:
        //     User is idle.
        Idle = idle,
        //
        // Zusammenfassung:
        //     User asked not to be disturbed.
        DoNotDisturb = dnd,
        //
        // Zusammenfassung:
        //     User is invisible. They will appear as Offline to anyone but themselves.
        Invisible = invis
        --
        Activities:     
        //
        // Zusammenfassung:
        //     Indicates the user is playing a game.
        Playing = 0,
        //
        // Zusammenfassung:
        //     Indicates the user is streaming a game.
        Streaming = 1, (Should not work)
        //
        // Zusammenfassung:
        //     Indicates the user is listening to something.
        ListeningTo = 2,
        //
        // Zusammenfassung:
        //     Indicates the user is watching something.
        Watching = 3,
        //
        // Zusammenfassung:
        //     Indicates the current activity is a custom status.
        Custom = 4, (Should not work)
        //
        // Zusammenfassung:
        //     Indicates the user is competing in something.
        Competing = 5