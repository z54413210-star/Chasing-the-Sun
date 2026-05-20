# Level3 双人镜像关卡完整实现方案

## Summary
Level3 不使用世界切换，也不依赖 `PresentPastSceneManager`。这一关采用双人分屏、左右镜像解谜，分为两层能力：

- 基础能力：普通平台、普通箱子、梯子、营火检查点、掉落死亡、黑幕淡入淡出
- 特殊机关：镜像箱子对、镜像守卫对、压力板控制平台显隐

实现约束保持不变：不修改任何现有脚本，只新增脚本、prefab 和 Level3 场景接线。`Player1` 继续使用现有 `PlayerController2D`；`Player2` 新增一份完整同手感控制器，功能与 `Player1` 对齐，仅键位改为 `方向键 + 小键盘 0`。

## Key Changes
### 玩家与基础能力
- 保留 `Player1` 的 `PlayerController2D`、`PlayerAnimationDriver`、现有普通箱子推动逻辑不变。
- 新增 `PlayerTwoController2D`，完整复刻当前 `PlayerController2D` 的行为：
  - 左右移动、跳跃、普通箱子推动、梯子吸附/攀爬、落地状态、死亡与复活状态
  - 参数默认与现有 `PlayerController2D` 一致，确保手感统一
  - 输入改为：
    - `LeftArrow` / `RightArrow`：左右移动
    - `UpArrow` / `DownArrow`：爬梯
    - `Keypad0`：跳跃
- 新增 `PlayerTwoLadderTraversalAssist`，对应 `PlayerTwoController2D`，完整复制现有梯子穿行辅助行为。
- 新增 `Level3PlayerAvatar`，挂在两个玩家上作为第三关统一入口：
  - 标记左右侧 `Left/Right`
  - 提供统一访问：Transform、脚底位置、当前刚体、`Kill()`、`RespawnAt()`、`SetCheckpoint()`
  - `Player1` 适配现有控制器，`Player2` 适配新控制器
- 新增 `Level3PlayerLife`，分别挂在两个玩家上：
  - 记录默认出生点和本侧最近营火检查点
  - 管理死亡流程：暂时冻结玩家、播放本半屏黑幕淡入淡出、回到最近检查点
  - 掉出死亡线时同样触发死亡流程
  - 黑幕是暂时淡入淡出，不会保持全黑；时长默认沿用现有 `RespawnManager` 的节奏参数

### Level3 基础场景支持
- Level3 不使用现有 `RespawnManager` 作为主生命系统。
- 继续使用营火作为检查点外观，但新增 `Level3Checkpoint` 负责第三关检查点逻辑：
  - 左边营火只记录左玩家
  - 右边营火只记录右玩家
  - 不共享检查点
- 新增 `Level3DeathBoundary`：
  - 场景放一条或一个下边界触发区
  - 玩家掉出边界后触发各自死亡与复活
- 梯子继续可用，但需要新增 `PlayerTwoLadderZone`：
  - 现有 `LadderZone` 继续服务 `Player1`
  - `PlayerTwoLadderZone` 结构与其一致，改为识别 `PlayerTwoController2D`
  - Level3 的梯子对象同时挂两种脚本，确保两位玩家都能爬
- 普通箱子继续使用现有 `PushableBox` 体系：
  - `Player1` 直接兼容
  - `PlayerTwoController2D` 的推箱检测同样识别 `PushableBox`
  - 无需为普通箱子再做一套独立 prefab
- 平台本体不需要额外基础脚本，继续作为普通地形使用；仅“会被压力板控制的目标平台”需要新控制脚本。

### 镜像箱子对 prefab
- 新增 `MirrorBoxUnit`：
  - 继承 `PushableBox`，保留普通物理和被推动能力
  - 额外记录所属 pair、左右侧、镜像状态
- 新增 `MirrorBoxPairController`：
  - 管理一对关于屏幕中线对称的箱子
  - 定义一条 `mirrorCenterX`
  - 任意一侧箱子被推动时，另一侧按镜像方向做同步水平位移
  - 两个箱子共享一个自由度，不允许不同步漂移
  - 采用物理横向联动风格，但真正的位置同步由 pair controller 统一约束
  - 任意一侧若检测到非玩家障碍物阻挡，则两边同步停止
  - 若两边同时施加相反方向推动，则净效果为 0
- prefab 结构：
  - `MirrorBoxPairRoot`
  - 子物体：`LeftBox`、`RightBox`
  - 图片未到位前使用占位 Sprite；碰撞体尺寸和逻辑根先按标准箱子尺寸配置

### 镜像守卫对 prefab
- 新增 `GhostPlatformSensor`：
  - 挂在每个守卫所属平台上
  - 记录当前处于平台触发范围内的本侧玩家
- 新增 `MirrorGhostPairController`：
  - 管理一对守卫与一对平台
  - 参数：左右平台边界、守卫引用、镜像中线、移动速度、接触判定
  - 行为规则：
    - 两个平台都没人：两个守卫停在当前点待机
    - 单侧有人：该侧守卫追向本侧玩家，另一侧守卫镜像跟随
    - 双侧都有人：分别计算两边“守卫到本侧最近玩家”的距离，距离更短的一侧主导
    - 若距离相同，沿用上一帧主导侧，避免抖动
    - 守卫只在自己平台范围内水平移动
    - 若前方碰到非玩家 Collider，则两个守卫同步停下
    - 守卫接触玩家时，调用该玩家的 `Level3PlayerLife.Kill()`
- prefab 结构：
  - `MirrorGhostPairRoot`
  - 子物体：`LeftGhost`、`RightGhost`、`LeftSensor`、`RightSensor`
  - 推荐把两侧平台边界引用做成可视化 Gizmo，方便摆关卡

### 压力板与平台显隐
- 新增 `PressurePlateTrigger`：
  - 统计压在板上的 `Level3PlayerAvatar`
  - 只要有人踩住就视为激活
  - 离开则失活
- 新增 `TargetPlatformActiveController`：
  - 控制目标平台 `SetActive`
  - 配置 `defaultActive` 与 `pressedActive`
  - 规则固定为“踩住才生效”，离开恢复原状态
- 平台显隐只改目标平台根物体激活状态，不额外做渐变或延迟。
- 如果平台消失导致玩家掉出死亡线，则由 `Level3DeathBoundary` 统一判死。

### 分屏与黑幕
- 场景保留双相机：
  - `LeftCamera` 跟随 `Player1`
  - `RightCamera` 跟随 `Player2`
- 新增两块半屏 `FadeOverlay`：
  - 左半屏 overlay 只给 `Player1`
  - 右半屏 overlay 只给 `Player2`
- 淡入淡出流程：
  - 死亡时对应半屏淡入黑幕
  - 玩家复位到最近营火
  - 再淡出恢复画面
- 另一位玩家在同伴死亡时继续操作，不会整屏一起黑。

## Public Interfaces / Types
- `enum Level3Side { Left, Right }`
- `Level3PlayerAvatar`
- `Level3PlayerLife`
- `PlayerTwoController2D`
- `PlayerTwoLadderTraversalAssist`
- `PlayerTwoLadderZone`
- `Level3Checkpoint`
- `Level3DeathBoundary`
- `MirrorBoxUnit`
- `MirrorBoxPairController`
- `GhostPlatformSensor`
- `MirrorGhostPairController`
- `PressurePlateTrigger`
- `TargetPlatformActiveController`

## Prefab Plan
- 新增 `CTS_Player_P2` prefab
  - 结构尽量复制现有 `CTS_Player`
  - 替换为 `PlayerTwoController2D + PlayerTwoLadderTraversalAssist + Level3PlayerAvatar + Level3PlayerLife`
- 新增 `MirrorBoxPair` prefab
- 新增 `MirrorGhostPair` prefab
- 新增 `PressurePlate` prefab
- 目标平台继续使用普通平台 prefab/场景物体，只补 `TargetPlatformActiveController`
- 检查点继续使用营火资源外观，在第三关场景中挂 `Level3Checkpoint`

## Test Plan
- `Player1` 与 `Player2` 都能正常：
  - 站立、移动、跳跃
  - 推普通箱子
  - 爬普通梯子
  - 触发营火检查点
  - 死亡并淡入淡出复活
- `Player2` 键位只响应 `方向键 + 小键盘 0`，不影响 `Player1`
- 镜像箱子：
  - 左推右同步镜像，右推左同步镜像
  - 一边被非玩家障碍阻挡时两边都停
- 镜像守卫：
  - 无玩家时待机
  - 单侧有人时单侧主导追击
  - 双侧有人时距离更短侧主导
  - 守卫遇到非玩家障碍时同步停止
  - 接触玩家时只该玩家死亡并半屏黑幕
- 压力板：
  - 踩住目标平台切换显隐
  - 离开恢复
  - 平台消失导致玩家掉出死亡线时正常判死
- 左右营火分别只更新本侧玩家的复活点

## Assumptions
- Level3 会同时使用“普通箱子/梯子/平台”和“镜像特殊机关”两套内容。
- 普通箱子继续沿用现有 `PushableBox` 思路，不额外重做。
- 营火继续作为检查点外观，但第三关检查点逻辑独立实现。
- 所有新增逻辑通过新脚本和新 prefab 完成，不修改任何现有脚本文件。



现在你需要在 Unity 里手动接线

Player1 物体上加：
Level3PlayerAvatar.cs
Level3PlayerLife.cs
然后把 Side 设为 Left
Player2 直接拖 CTS_Player_P2.prefab 进场景
每个梯子同时挂两个脚本：
现有的 LadderZone
新的 PlayerTwoLadderZone.cs
每个营火挂 Level3Checkpoint.cs，左右分别设 Side
场景下方死亡区挂 Level3DeathBoundary.cs
两个半屏黑幕要各自有 FadeOverlay，并且名字里最好包含 Left / Right，这样生命脚本会自动找
MirrorBoxPair 放进场景后，按你的中线设置 mirrorCenterX
MirrorGhostPair 放进场景后，设置 mirrorCenterX、左右平台可移动边界
要被压力板控制的平台物体上挂 TargetPlatformActiveController.cs，把 sourcePlate 指到压力板，把 targetRoot 指到目标平台





Prefab Inspector 参数填写说明

我按“你真正需要手动填的”来写。
原则是：

必须填：不填就大概率不工作
通常不用动：prefab 里一般已经接好了
可调：按手感和关卡需要改
1. CTS_Player 场景中的 Player1
这不是新 prefab，一般是你把现有玩家拖进场景后，在场景实例上补 Level3 组件。

Level3PlayerAvatar
必须填

Side = Left
playerOneController = 拖 PlayerController2D
playerTwoController = 留空
body = 拖玩家的 Rigidbody2D
bodyCollider = 拖玩家主碰撞体
life = 拖本物体上的 Level3PlayerLife
Level3PlayerLife
必须填

avatar = 拖 Level3PlayerAvatar
fadeOverlay = 拖 LeftFadeOverlay
通常不用动

defaultSpawnPoint = 可留空
fadeDuration = 默认
blackScreenHoldDuration = 默认
Animator
必须检查

Controller = 你的 Player1 动画 controller
2. CTS_Player_P2.prefab
这是 Player2 用的 prefab。

PlayerTwoController2D
通常不用动

这上面的移动、跳跃、重力、推箱、爬梯参数，默认先保持和 prefab 一样
如果之后觉得手感不对，再调
PlayerTwoLadderTraversalAssist
通常不用动

一般 prefab 已经接好了
只有在引用丢失时才需要补
Level3PlayerAvatar
通常 prefab 已接好，但你要检查

Side = Right
playerOneController = 留空
playerTwoController = PlayerTwoController2D
body = Rigidbody2D
bodyCollider = 主碰撞体
life = Level3PlayerLife
Level3PlayerLife
必须填

fadeOverlay = 拖 RightFadeOverlay
通常不用动

avatar 一般已接好
其他保持默认
Animator
必须检查

Controller = Player2 自己那套 Animator Controller
不要和 Player1 共用同一个 controller
3. LeftCamera
Camera
必须填

Viewport Rect
X = 0
Y = 0
W = 0.5
H = 1
必须检查

Orthographic = 开
Culling Mask = 不要把场景层过滤掉
CameraFollow2D
必须填

target = Player1
offset 建议 (0, 0, -10)
通常不用动

useBounds = 默认
smoothTime = 默认
4. RightCamera
Camera
必须填

Viewport Rect
X = 0.5
Y = 0
W = 0.5
H = 1
CameraFollow2D
必须填

target = Player2
offset 建议 (0, 0, -10)
5. Main Camera
必须做

GameObject 直接禁用
6. LeftFadeOverlay
这是 Canvas 下的左半屏 Image。

RectTransform
必须填

Anchor 拉成左半屏
Min = (0, 0)
Max = (0.5, 1)
Image
必须填

颜色 = 黑色
CanvasGroup
通常不用动

初始透明度 0
FadeOverlay
通常不用动

canvasGroup = 拖本物体上的 CanvasGroup
7. RightFadeOverlay
RectTransform
必须填

Anchor 拉成右半屏
Min = (0.5, 0)
Max = (1, 1)
Image
必须填

颜色 = 黑色
FadeOverlay
通常不用动

canvasGroup = 本物体的 CanvasGroup
8. 普通平台 prefab / 平台场景物体
如果你直接用普通地形 prefab：

Transform
必须调

位置
缩放
BoxCollider2D
必须检查

顶面要平
高度不要过厚，否则角色脚感会怪
没有额外脚本要填

9. Box_Pushable.prefab 普通箱子
Transform
必须调

放置位置
BoxCollider2D
必须检查

要完整包住箱子
不要只有中间一小块
PushableBox
通常不用动

prefab 默认参数先别改
Rigidbody2D
通常不用动

10. Ladder_Climbable.prefab
现有 LadderZone
通常 prefab 已接好

不建议乱动
你要额外加一个 PlayerTwoLadderZone
这是 Player2 能爬梯的关键。

必须填

ladderCollider = 拖梯子自己的触发 Collider
BoxCollider2D
必须检查

Is Trigger = 开
高度足够覆盖整架梯子
11. 营火 prefab 场景实例
你可以继续用营火 prefab 做外观。

旧的 CampfireCheckpoint
建议

在 Level3 里禁用，或者删掉
新加 Level3Checkpoint
必须填

左边营火：

side = Left
右边营火：

side = Right
可选

spawnPoint = 专门拖一个空物体作为复活点
如果不填，就用 spawnOffset
通常不用动

spawnOffset 默认先用
营火 Collider
必须检查

Is Trigger = 开
玩家能碰到
12. Level3DeathBoundary
这通常不是 prefab，而是你自己建的场景物体。

BoxCollider2D
必须填

Is Trigger = 开
宽度横跨地图
Level3DeathBoundary
不用填参数

这个脚本没有 Inspector 参数
13. MirrorBoxPair.prefab
MirrorBoxPairController
必须填

mirrorCenterX = 这组镜像机关的中线 x 坐标
通常不用动

leftBox = prefab 一般已接好
rightBox = prefab 一般已接好
inputDeadZone = 默认先别改
LeftBox / RightBox 上的 MirrorBoxUnit
通常不用动

attachedBody 一般已接好
pairController 一般已接好
side 一般已接好
boxCollider 一般已接好
你手动要检查的不是参数，而是摆放
必须检查

左右箱子是否关于 mirrorCenterX 对称
两边平台高度一致
初始没有卡进平台
前方有推动空间
14. MirrorGhostPair.prefab
MirrorGhostPairController
必须填

mirrorCenterX
leftMinX
leftMaxX
rightMinX
rightMaxX
可调

moveSpeed
通常不用动

leftGhost
rightGhost
leftGhostCollider
rightGhostCollider
leftSensor
rightSensor
这些一般 prefab 已经接好。

GhostPlatformSensor
通常不用动

左边 sensor 应该是 acceptedSide = Left
右边 sensor 应该是 acceptedSide = Right
你手动最需要做的是
必须检查

左右平台长度和边界一致
leftMinX / leftMaxX 真正落在左平台上
rightMinX / rightMaxX 真正落在右平台上
传感器范围能覆盖玩家站立区域
15. PressurePlate.prefab
PressurePlateTrigger
不用手填

pressed 是运行时值
你不用在 Inspector 里配它
BoxCollider2D
必须检查

Is Trigger = 开
玩家能踩到
Transform
必须调

放在正确地面高度
别埋进平台里
16. 被压力板控制的目标平台
这个通常不是独立 prefab，而是你放进场景里的普通平台。

TargetPlatformActiveController
必须填

sourcePlate = 拖对应压力板上的 PressurePlateTrigger
targetRoot = 拖目标平台自己
defaultActive
pressedActive
常见填法
默认隐藏，踩下显示：

defaultActive = false
pressedActive = true
默认显示，踩下消失：

defaultActive = true
pressedActive = false
17. 最容易漏的参数表
你可以照这个一项项核对：

Player1
Level3PlayerAvatar.side = Left
Level3PlayerAvatar.playerOneController = PlayerController2D
Level3PlayerLife.fadeOverlay = LeftFadeOverlay
Player2
Level3PlayerAvatar.side = Right
Level3PlayerAvatar.playerTwoController = PlayerTwoController2D
Level3PlayerLife.fadeOverlay = RightFadeOverlay
Animator.Controller = Player2 那套
LeftCamera
Viewport Rect = (0,0,0.5,1)
CameraFollow2D.target = Player1
RightCamera
Viewport Rect = (0.5,0,0.5,1)
CameraFollow2D.target = Player2
梯子
保留原 LadderZone
再加 PlayerTwoLadderZone
PlayerTwoLadderZone.ladderCollider = 梯子的 trigger collider
营火
加 Level3Checkpoint
左边 side = Left
右边 side = Right
镜像箱子
MirrorBoxPairController.mirrorCenterX = 该工位中线
镜像守卫
mirrorCenterX
leftMinX / leftMaxX
rightMinX / rightMaxX
moveSpeed
压力板控制平台
sourcePlate = 对应 PressurePlateTrigger
targetRoot = 目标平台
defaultActive / pressedActive 按机关逻辑设置
