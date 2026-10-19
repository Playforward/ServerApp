using ServerShared.Titles;

namespace ServerApp;

internal static class DefaultTitles
{
    public static IReadOnlyList<TitleDescriptor> All { get; } =
    [
        new()
        {
            TitleId = "farcry3",
            Name = "Far Cry 3",
            ResourceDir = "Titles/FarCry3",
            ProtocolFamily = "farcry3",
            Matchers = new()
            {
                LegacyOnlineConfigId = "21fc0b75f83c49f2a51726f60fd1c5f8",
                Ports = [6570, 6571],
            },
        },
        new()
        {
            TitleId = "splinter-cell-blacklist",
            Name = "Splinter Cell Blacklist",
            ResourceDir = "Titles/SplinterCellBlacklist",
            ProtocolFamily = "overlord",
            Matchers = new()
            {
                LegacyOnlineConfigId = "967fad701a3648d8bf099f07207f4a73",
                AccessKey = "yl4NG7qZ",
                Ports = [6572, 6573],
            },
        },
        new()
        {
            TitleId = "ghost-recon-future-soldier",
            Name = "Ghost Recon Future Soldier",
            ResourceDir = "Titles/GhostReconFutureSoldier",
            ProtocolFamily = "futuresoldier",
            Matchers = new()
            {
                LegacyOnlineConfigId = "23ad683803a0457cabce83f905811dbc",
                AccessKey = "8dtRv2oj",
                Ports = [6574, 6575],
            },
        },
        new()
        {
            TitleId = "assassins-creed-brotherhood",
            Name = "Assassin's Creed Brotherhood",
            ResourceDir = "Titles/AssassinsCreedBrotherhood",
            ProtocolFamily = "brotherhood",
            Matchers = new()
            {
                LegacyOnlineConfigId = "f26ad290e85146a685acbbb5f6ed7672",
                AccessKey = "UjhTgf85",
                Ports = [6576, 6577],
            },
        },
        new()
        {
            TitleId = "farcry4",
            Name = "Far Cry 4",
            ResourceDir = "Titles/FarCry4",
            Matchers = new()
            {
                AppId = "2d10b938-06a4-4f79-89b5-a57706032a5f",
                SpaceId = "aab6ceb2-2ea5-4d27-9719-cc2cb87395b7",
            },
        },
        new()
        {
            TitleId = "farcry5",
            Name = "Far Cry 5",
            ResourceDir = "Titles/FarCry5",
            Matchers = new()
            {
                AppId = "e0b29a98-ce38-4bc7-96ee-9d20e793e9e3",
                SpaceId = "f40e304d-8e8d-4343-8270-d06487c35add",
            },
        },
        new()
        {
            TitleId = "grand-theft-auto-iv",
            Name = "Grand Theft Auto IV",
            ResourceDir = "Titles/GTAIV",
            ProtocolFamily = "ros",
        },
        new()
        {
            TitleId = "farcry-new-dawn",
            Name = "Far Cry New Dawn",
            ResourceDir = "",
            Matchers = new()
            {
                AppId = "4eb9089b-324e-4e65-a055-5ea05cfeb68f",
                SpaceId = "6f75883b-3b5b-45ce-a2d4-2a7a8477180c",
            },
        },
    ];
}
