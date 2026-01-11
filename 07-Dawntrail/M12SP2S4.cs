using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Dalamud.Utility.Numerics;
using KodakkuAssist.Script;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.Draw.Manager;
using KodakkuAssist.Data;

namespace Cyf5119Scripts.Dawntrail.M12SP2S4;

[ScriptType(guid: "C83916CE-5BBD-9326-4F44-47FD7CD930B8", name: "M12S本体四运粗绘", territorys: [1327], version: "0.0.0.1", author: "Cyf5119", note: Note, updateInfo: Info)]
public class M12SP2S4
{
    private const string Note = "M12S本体四运粗绘，详细画图请等灵视，首周加油。";
    private const string Info = "空";
    
    private static readonly Vector3 Center = new(100, 0, 100);
    private static uint num = 0;
    private static bool isAxisFisrt = false;
    private static Dictionary<uint, Vector3> playerShadowPos = new();
    private static Dictionary<uint, byte> playerShapes = new();
    private static Dictionary<uint, byte> playerIndex = new();
    private static Dictionary<uint, byte> addShapes = new();

    public void Init(ScriptAccessory sa)
    {
        num = 0;
    }

    private void Reset()
    {
        isAxisFisrt = false;
        playerShadowPos.Clear();
        playerShapes.Clear();
        playerIndex.Clear();
        addShapes.Clear();
    }

    [ScriptMethod(name: "境中奇梦计数", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:46345"], userControl: false)]
    public void 境中奇梦计数(Event evt, ScriptAccessory sa)
    {
        num++;
        Reset();
    }

    private bool PhaseCheck() => num != 1;

    
    #region 影子连线

    // 两批影子间隔 2s
    [ScriptMethod(name: "玩家影子刷出", eventType: EventTypeEnum.PlayActionTimeline, eventCondition: ["Id:4562", "SourceDataId:19210"], suppress: 5000, userControl: false)]
    public void 玩家影子刷出(Event evt, ScriptAccessory sa)
    {
        if (PhaseCheck()) return;
        isAxisFisrt = (evt.SourcePosition().V3YAngle(Center) + 22.5) % 90 < 45;
    }

    [ScriptMethod(name: "玩家影子连线", eventType: EventTypeEnum.Tether, eventCondition: ["Id:0175"], userControl: false)]
    public void 玩家影子连线(Event evt, ScriptAccessory sa)
    {
        if (PhaseCheck()) return;
        var obj = sa.Data.Objects.SearchById(evt.SourceId());
        if (obj == null || obj.DataId != 19210) return;
        lock (playerShadowPos)
        {
            playerShadowPos[evt.TargetId()] = evt.SourcePosition();
        }
    }
    
    [ScriptMethod(name: "玩家接线记录", eventType: EventTypeEnum.Tether, eventCondition: ["Id:regex:^(017[01])$"], userControl: false)]
    public void 玩家接线记录(Event evt, ScriptAccessory sa)
    {
        if (PhaseCheck()) return;
        byte shape = evt["Id"] switch
        {
            "0170" => 0, // 20大圈
            "0171" => 1, // 5分摊
            _      => 2
        };
        lock (playerShapes)
        {
            playerShapes[evt.TargetId()] = shape;
            playerIndex[evt.TargetId()] = (byte)((360 - evt.SourcePosition().V3YAngle(Center) + 22.5) % 180 / 45);
        }
    }

    [ScriptMethod(name: "分身->玩家 分摊大圈", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:46377"])]
    public void 分身玩家分摊大圈(Event evt, ScriptAccessory sa)
    {
        if (PhaseCheck()) return;
        foreach (var pid in sa.Data.PartyList)
        {
            var shape = playerShapes[pid];
            var index = playerIndex[pid];
            var delay = index switch
            {
                0 => 11100,
                1 => 17500,
                2 => 21200,
                3 => 27400,
                _ => 0
            };
            var dp = sa.FastDp("分身玩家分摊大圈", pid, 6300, shape > 0 ? 5 : 20, shape > 0);
            dp.Delay = delay;
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }

    [ScriptMethod(name: "分身->影子 分摊大圈", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:46365", "TargetIndex:1"])]
    public void 分身影子分摊大圈(Event evt, ScriptAccessory sa)
    {
        if (PhaseCheck()) return;
        foreach (var pid in sa.Data.PartyList)
        {
            var shape = playerShapes[pid];
            var shadowPos = playerShadowPos[pid];
            var isFirst = isAxisFisrt == ((shadowPos.V3YAngle(Center) + 22.5) % 90 < 45);
            var dp = sa.FastDp("dayingzi", shadowPos, 10700, shape > 0 ? 5 : 20, shape > 0);
            dp.Delay = isFirst ? 8400 : 29400;
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }

    #endregion


    #region 小怪组合技
    
    [ScriptMethod(name: "小怪形状记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(4635[123])$"], userControl: false)]
    public void 小怪形状记录(Event evt, ScriptAccessory sa)
    {
        if (PhaseCheck()) return;
        byte shape = evt.ActionId() switch
        {
            46351 => 1, // 两侧扇形
            46352 => 2, // 前后扇形
            46353 => 3, // 钢铁
            _     => 4
        };
        lock (addShapes)
        {
            addShapes[evt.SourceId()] = shape;
        }
    }

    private static void DrawAdds(ScriptAccessory sa, uint id, uint duration, uint delay = 0)
    {
        var shape = addShapes[id];
        var dp = sa.FastDp("扇形钢铁组合技", id, duration, shape > 2 ? 10 : 60);
        dp.Delay = delay;
        switch (shape)
        {
            case 1:
                dp.Rotation = float.Pi / 2;
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
                dp.Rotation = -float.Pi / 2;
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
                break;
            case 2:
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
                dp.Rotation = float.Pi;
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
                break;
            case 3:
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                break;
            default:
                return;
        }
    }

    [ScriptMethod(name: "第一次扇形钢铁组合", eventType: EventTypeEnum.Tether, eventCondition: ["Id:regex:^(017[01])$"], suppress: 10000)]
    public void 第一次扇形钢铁组合(Event evt, ScriptAccessory sa)
    {
        if (PhaseCheck()) return;
        foreach (var (sid, shape) in addShapes)
            DrawAdds(sa, sid, 12740);
    }

    [ScriptMethod(name: "第二次扇形钢铁组合", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:46297", "TargetIndex:1"])]
    public void 第二次扇形钢铁组合(Event evt, ScriptAccessory sa)
    {
        if (PhaseCheck()) return;
        DrawAdds(sa, evt.SourceId(), 8600, 17600);
    }

    [ScriptMethod(name: "第三次扇形钢铁组合", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:46366", "TargetIndex:1"])]
    public void 第三次扇形钢铁组合(Event evt, ScriptAccessory sa)
    {
        if (PhaseCheck()) return;
        DrawAdds(sa, evt.SourceId(), 4800, 1500);
    }

    #endregion


    #region 踩塔

    [ScriptMethod(name: "踩塔提示", eventType: EventTypeEnum.ObjectChanged, eventCondition: ["DataId:regex:^(201501[3456])$", "Operate:Add"])]
    public void 踩塔提示(Event evt, ScriptAccessory sa)
    {
        if (PhaseCheck()) return;
        switch (evt.DataId())
        {
            case 2015013: // 风
                var dpFeng = sa.FastDp("feng", evt.SourcePosition(), 5000, new Vector2(2, 50), true);
                dpFeng.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
                dpFeng.TargetOrderIndex = 1;
                dpFeng.Delay = 47400;
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dpFeng);
                break;
            case 2015014: // 暗
                var dpAn = sa.FastDp("an", evt.SourcePosition(), 5000, new Vector2(10, 50));
                dpAn.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
                dpAn.TargetOrderIndex = 1;
                dpAn.Delay = 47400;
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dpAn);
                break;
            case 2015015: // 土
            case 2015016: // 火
                break;
        }
    }

    [ScriptMethod(name: "土塔垒石", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:46327"])]
    public void 土塔垒石(Event evt, ScriptAccessory sa)
    {
        if (PhaseCheck()) return;
        var dp = sa.FastDp("土塔垒石", evt.SourceId(), 5000, 4);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "远近扇形", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(476[67])$"])]
    public void 远近扇形(Event evt, ScriptAccessory sa)
    {
        if (PhaseCheck()) return;
        var dp = sa.FastDp("远近扇形", evt.TargetId(), 5000, 60);
        dp.Radian = float.Pi / 6;
        dp.Delay = 5000;
        dp.TargetResolvePattern = evt.StatusId > 4766 ? PositionResolvePatternEnum.PlayerNearestOrder : PositionResolvePatternEnum.PlayerFarestOrder;
        dp.TargetOrderIndex = 1;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    #endregion
}

#region Helpers

public static class EventExtensions
{
    private static bool ParseHexId(string? idStr, out uint id)
    {
        id = 0;
        if (string.IsNullOrEmpty(idStr)) return false;
        try
        {
            var idStr2 = idStr.Replace("0x", "");
            id = uint.Parse(idStr2, System.Globalization.NumberStyles.HexNumber);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static uint Id(this Event evt) => JsonConvert.DeserializeObject<uint>(evt["Id"]);
    public static uint ActionId(this Event evt) => JsonConvert.DeserializeObject<uint>(evt["ActionId"]);
    public static uint SourceId(this Event evt) => ParseHexId(evt["SourceId"], out var id) ? id : 0;
    public static uint TargetId(this Event evt) => ParseHexId(evt["TargetId"], out var id) ? id : 0;
    public static uint IconId(this Event evt) => ParseHexId(evt["Id"], out var id) ? id : 0;
    public static Vector3 SourcePosition(this Event evt) => JsonConvert.DeserializeObject<Vector3>(evt["SourcePosition"]);
    public static Vector3 TargetPosition(this Event evt) => JsonConvert.DeserializeObject<Vector3>(evt["TargetPosition"]);
    public static Vector3 EffectPosition(this Event evt) => JsonConvert.DeserializeObject<Vector3>(evt["EffectPosition"]);
    public static float SourceRotation(this Event evt) => JsonConvert.DeserializeObject<float>(evt["SourceRotation"]);
    public static float TargetRotation(this Event evt) => JsonConvert.DeserializeObject<float>(evt["TargetRotation"]);
    public static string SourceName(this Event evt) => evt["SourceName"];
    public static string TargetName(this Event evt) => evt["TargetName"];
    public static uint DurationMilliseconds(this Event evt) => JsonConvert.DeserializeObject<uint>(evt["DurationMilliseconds"]);
    public static uint Index(this Event evt) => ParseHexId(evt["Index"], out var id) ? id : 0;
    public static uint State(this Event evt) => ParseHexId(evt["State"], out var id) ? id : 0;
    public static uint DirectorId(this Event evt) => ParseHexId(evt["DirectorId"], out var id) ? id : 0;
    public static uint StatusId(this Event evt) => JsonConvert.DeserializeObject<uint>(evt["StatusID"]);
    public static uint StackCount(this Event evt) => JsonConvert.DeserializeObject<uint>(evt["StackCount"]);
    public static uint Param(this Event evt) => JsonConvert.DeserializeObject<uint>(evt["Param"]);
    public static uint TargetIndex(this Event evt) => JsonConvert.DeserializeObject<uint>(evt["TargetIndex"]);
    public static uint DataId(this Event evt) => JsonConvert.DeserializeObject<uint>(evt["DataId"]);
}

public static class ScriptAccessoryExtensions
{
    public static DrawPropertiesEdit FastDp(this ScriptAccessory sa, string name, uint owner, uint duration, float radius, bool safe = false)
    {
        return FastDp(sa, name, owner, duration, new Vector2(radius), safe);
    }

    public static DrawPropertiesEdit FastDp(this ScriptAccessory sa, string name, uint owner, uint duration, Vector2 scale, bool safe = false)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = safe ? sa.Data.DefaultSafeColor : sa.Data.DefaultDangerColor;
        dp.Owner = owner;
        dp.DestoryAt = duration;
        dp.Scale = scale;
        return dp;
    }

    public static DrawPropertiesEdit FastDp(this ScriptAccessory sa, string name, Vector3 pos, uint duration, float radius, bool safe = false)
    {
        return FastDp(sa, name, pos, duration, new Vector2(radius), safe);
    }

    public static DrawPropertiesEdit FastDp(this ScriptAccessory sa, string name, Vector3 pos, uint duration, Vector2 scale, bool safe = false)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = safe ? sa.Data.DefaultSafeColor : sa.Data.DefaultDangerColor;
        dp.Position = pos;
        dp.DestoryAt = duration;
        dp.Scale = scale;
        return dp;
    }

    public static DrawPropertiesEdit WaypointDp(this ScriptAccessory sa, uint target, uint duration, uint delay = 0, string name = "Waypoint")
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = sa.Data.DefaultSafeColor;
        dp.Owner = sa.Data.Me;
        dp.TargetObject = target;
        dp.DestoryAt = duration;
        dp.Delay = delay;
        dp.Scale = new Vector2(2);
        dp.ScaleMode = ScaleMode.YByDistance;
        return dp;
    }

    public static DrawPropertiesEdit WaypointDp(this ScriptAccessory sa, Vector3 pos, uint duration, uint delay = 0, string name = "Waypoint")
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = sa.Data.DefaultSafeColor;
        dp.Owner = sa.Data.Me;
        dp.TargetPosition = pos;
        dp.DestoryAt = duration;
        dp.Delay = delay;
        dp.Scale = new Vector2(2);
        dp.ScaleMode = ScaleMode.YByDistance;
        return dp;
    }

    public static int MyIndex(this ScriptAccessory sa)
    {
        return sa.Data.PartyList.IndexOf(sa.Data.Me);
    }

    public static IEnumerable<IPlayerCharacter> GetParty(this ScriptAccessory sa)
    {
        foreach (var pid in sa.Data.PartyList)
        {
            var obj = sa.Data.Objects.SearchByEntityId(pid);
            if (obj is IPlayerCharacter character) yield return character;
        }
    }

    public static IPlayerCharacter? GetMe(this ScriptAccessory sa)
    {
        return (IPlayerCharacter?)sa.Data.Objects.SearchByEntityId(sa.Data.Me);
    }
}

public static class MathHelper
{
    public static float V3YAngle(this Vector3 v, bool toRadian = false)
    {
        return V3YAngle(v, Vector3.Zero, toRadian);
    }

    public static float V3YAngle(this Vector3 v, Vector3 origin, bool toRadian = false)
    {
        var angle = ((MathF.Atan2(v.Z - origin.Z, v.X - origin.X) - MathF.Atan2(1, 0)) / float.Pi * -180 + 360) % 360;
        return toRadian ? angle / 180 * float.Pi : angle;
    }

    public static Vector3 V3YRotate(this Vector3 v, float angle, bool isRadian = false)
    {
        return V3YRotate(v, Vector3.Zero, angle, isRadian);
    }

    public static Vector3 V3YRotate(this Vector3 v, Vector3 origin, float angle, bool isRadian = false)
    {
        var radian = isRadian ? angle : angle / 180 * float.Pi;
        return Vector3.Transform(v - origin, Matrix4x4.CreateRotationY(radian)) + origin;
    }
}

#endregion