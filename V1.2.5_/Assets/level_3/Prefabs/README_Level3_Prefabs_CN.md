# Level3 Prefab 搭图说明

这份文档是给负责搭 Level3 地图的同学用的。
目标是：看到 prefab 后，知道它是干什么的、应该怎么摆、Inspector 里哪些参数必须检查。

## 1. Prefab 目录

主要使用下面这些资源：

- `Assets/level3/Prefabs/Player/CTS_Player_P2.prefab`
- `Assets/level3/Prefabs/Gameplay/Box_Pushable.prefab`
- `Assets/level3/Prefabs/Gameplay/Campfire_Checkpoint.prefab`
- `Assets/level3/Prefabs/Gameplay/MirrorBoxPair.prefab`
- `Assets/level3/Prefabs/Gameplay/MirrorGhostPair.prefab`
- `Assets/level3/Prefabs/Gameplay/paltformcontroller.prefab`
- `Assets/level3/Prefabs/Gameplay/Level3_StairPuzzle_4Step.prefab`
- `Assets/level3/Prefabs/Gameplay/PressurePlate.prefab`

说明：`paltformcontroller.prefab` 这个名字是现有资源名，虽然拼写有误，但先不要改名，避免引用断掉。

## 2. 推荐搭图顺序

建议按这个顺序搭：

1. 先把双相机、两个玩家、死亡线、黑幕 Overlay 接好。
2. 再铺普通地形、梯子、普通箱子、营火检查点。
3. 然后再放特殊机关：镜像箱子、镜像守卫、四层楼梯机关。
4. 最后统一测试玩家移动、爬梯、复活、压力板、守卫追击。

## 3. 基础场景接线

这些不是单个 prefab 自己能全包的，搭新 scene 时要手动做。

### 3.1 Player1

场景里的 Player1 继续用现有玩家对象，不需要换 prefab。
需要额外挂两个组件：

- `Level3PlayerAvatar`
- `Level3PlayerLife`

Inspector 必查：

`Level3PlayerAvatar`
- `Side = Left`
- `Player One Controller = 场景里的 PlayerController2D`
- `Player Two Controller = 空`
- `Body = 玩家 Rigidbody2D`
- `Body Collider = 玩家主碰撞体`
- `Life = 本物体上的 Level3PlayerLife`

`Level3PlayerLife`
- `Avatar = 本物体上的 Level3PlayerAvatar`
- `Fade Overlay = LeftFadeOverlay`
- `Default Spawn Point = 可空，不填就用初始出生点`

### 3.2 Player2

直接拖：

- `Assets/level3/Prefabs/Player/CTS_Player_P2.prefab`

默认已经接了 `PlayerTwoController2D`、`PlayerTwoLadderTraversalAssist`、`Level3PlayerAvatar`、`Level3PlayerLife`。

Player2 键位：
- 左右移动：方向键 `Left / Right`
- 爬梯：方向键 `Up / Down`
- 跳跃：小键盘 `0`

### 3.3 双相机

建议保留：
- `LeftCamera` 看左半屏
- `RightCamera` 看右半屏

常用 viewport：
- `LeftCamera`: `x=0, y=0, width=0.5, height=1`
- `RightCamera`: `x=0.5, y=0, width=0.5, height=1`

### 3.4 黑幕 Overlay

需要两个全屏 UI Image：
- `LeftFadeOverlay`
- `RightFadeOverlay`

名字里最好包含 `Left` / `Right`，这样 `Level3PlayerLife` 会自动找到对应黑幕。

初始透明度要是 `0`，不要一开始就是黑的。

### 3.5 死亡线

场景底部放一个触发区，挂：
- `Level3DeathBoundary`

玩家掉下去后，会调用各自的 `Level3PlayerLife`。

### 3.6 梯子

每个梯子对象要同时挂：
- 现有 `LadderZone`
- 新的 `PlayerTwoLadderZone`

这样 P1 和 P2 都能爬。

## 4. 基础 Prefab 说明

### 4.1 `Box_Pushable.prefab`

用途：普通箱子。

使用方法：
- 直接拖进场景
- 放到平台上
- 确保 BoxCollider 不要卡进平台里面

说明：
- P1、P2 都可以推动它
- 压力板也能识别普通箱子

### 4.2 `Campfire_Checkpoint.prefab`

用途：检查点外观。

要额外挂脚本：
- `Level3Checkpoint`

Inspector 必查：
- `Side = Left` 或 `Right`
- `Spawn Point = 可选`
- `Spawn Offset = 默认一般够用`

规则：
- 左营火只记左玩家
- 右营火只记右玩家

### 4.3 `paltformcontroller.prefab`

用途：一对普通左右镜像平台的摆放辅助。

内部逻辑：
- 根节点挂 `MirrorPlatformPlacementSync`
- 左右两个子平台会自动镜像摆位

适合场景：
- 你想快速摆一对左右对称的平台
- 但不需要额外机关逻辑，只要镜像位置

Inspector 必查：
- `localMirrorCenterX`：镜像中线，通常设为 `0`
- `syncWhilePlaying`：通常保持关，只在编辑器里同步

使用习惯：
- 只拖动一侧平台
- 另一侧会自动到镜像位置

## 5. 特殊机关 Prefab

### 5.1 `MirrorBoxPair.prefab`

用途：左右一对镜像箱子，任何一边推动，另一边镜像移动。

核心脚本：
- `MirrorBoxPairController`
- `MirrorBoxPlacementSync`
- 每个箱子上有 `MirrorBoxUnit`

Inspector 必查：

根节点 `MirrorBoxPairController`
- `mirrorCenterX`：镜像中线，通常设为这组机关的中心线
- `leftBox` / `rightBox`：prefab 里一般已经接好
- `inputDeadZone`：通常默认即可

根节点 `MirrorBoxPlacementSync`
- `localMirrorCenterX`：通常和上面的镜像中线保持一致
- `syncWhilePlaying`：通常关闭

搭法建议：
- 先把整组拖到目标区域
- 再只调整左箱子位置
- 右箱子会自动镜像过去
- 两侧前方最好都留出推箱空间
- 若一侧前面有墙，另一侧也最好做对应阻挡，便于测试

碰撞说明：
- 箱子 Collider 要完整包住箱体，不要只包中间一小块
- 放置时不要让箱子和平台初始重叠，否则容易出现卡住

### 5.2 `MirrorGhostPair.prefab`

用途：左右一对镜像守卫，在各自平台内追玩家。

核心脚本：
- `MirrorGhostPairController`
- `MirrorGhostPlacementSync`
- `GhostPlatformSensor`

Inspector 必查：

根节点 `MirrorGhostPairController`
- `mirrorCenterX`：镜像中线
- `leftGhost` / `rightGhost`：prefab 里一般已接好
- `leftGhostCollider` / `rightGhostCollider`：prefab 里一般已接好
- `leftSensor` / `rightSensor`：prefab 里一般已接好
- `useSensorBoundsForMovement = 建议开`
- `moveSpeed`：守卫速度，可按关卡难度调
- `useBlockingDetection = 目前建议关`，稳定性更高

如果 `useSensorBoundsForMovement` 开着：
- 左右守卫移动范围会直接取各自 sensor 的 Collider 边界
- 这时 `leftMinX/rightMinX` 这些数值不用手调

根节点 `MirrorGhostPlacementSync`
- `localMirrorCenterX`：通常和 `mirrorCenterX` 一样
- `mirrorSensors = 开`
- `syncWhilePlaying = 关`

Sensor 搭法：
- Sensor 的 Collider 要覆盖“守卫允许活动的平台区域”
- 玩家只要有一部分身体进入 sensor 区域，就会被检测到
- Sensor 不需要和 ghost 自己的 Collider 接触

Ghost Collider 搭法：
- 用 `BoxCollider2D`
- 覆盖幽灵本体的实际碰撞范围
- 不要做得特别宽，否则会出现看起来没碰到也被杀

### 5.3 `Level3_StairPuzzle_4Step.prefab`

用途：四层双板递进楼梯机关。

这是当前推荐给 Level3 使用的“上楼机关”。
不要再自己用嵌套父子物体硬搭，因为下层消失会把上层一起带没。

核心脚本：
- 根节点：`Level3DualStairPuzzleController`
- 每层：`Level3DualStairPairPlacementSync`
- 每个台阶根：`Level3FadeActiveGroup`

当前触发链固定为：
- 初始：`Left1`、`Right1` 存在
- `Left1` 压住 -> `Right2` 淡入；松开 -> `Right2` 淡出
- `Right2` 压住 -> `Left2` 淡入；松开 -> `Left2` 淡出
- `Left2` 压住 -> `Right3` 淡入；松开 -> `Right3` 淡出
- `Right3` 压住 -> `Left3` 淡入；松开 -> `Left3` 淡出
- `Left3` 压住 -> `Right4` 淡入；松开 -> `Right4` 淡出
- `Right4` 压住 -> `Left4` 淡入；松开 -> `Left4` 淡出

根节点 Inspector 必查：
- `Use Unified Fade Durations = 开`
- `Unified Fade In Duration Seconds`
- `Unified Fade Out Duration Seconds`

这两个值是统一控制整个楼梯机关所有平台淡入淡出时间的入口。

每层 `StepXPair` Inspector 必查：
- `localMirrorCenterX`：通常保持 `0`
- `syncWhilePlaying`：通常关

使用习惯：
- 整组拖进场景后，按层摆位置
- 每一层只调一边，另一边会镜像同步
- 不要把上层做成下层的子物体

说明：
- 当前 prefab 是固定 4 层版本
- 如果以后要扩成 5 层、6 层，需要同时扩 prefab 结构和控制脚本规则

### 5.4 `PressurePlate.prefab`

用途：旧版压力板资源。

注意：
- 这个 prefab 不是纯压力板
- 它内部带有旧的一对一平台控制逻辑
- 适合做参考或单个简单测试
- 不建议直接拿它去搭新的四层楼梯机关

如果要做新的楼梯机关，请优先使用：
- `Level3_StairPuzzle_4Step.prefab`

如果只是想要“人物/箱子踩一下，有个对象显隐”的简单机关，才考虑单独复用它或拆它的结构。

## 6. 玩家与机关兼容规则

### 6.1 压力板识别谁

当前 `PressurePlateTrigger` 识别：
- `Level3PlayerAvatar`
- `PushableBox`

也就是说：
- 玩家可以压板
- 普通箱子也可以压板

### 6.2 普通箱子与镜像箱子

- 普通解谜箱子：用 `Box_Pushable.prefab`
- 镜像联动箱子：用 `MirrorBoxPair.prefab`

这两种不要混成一个机关来理解。

### 6.3 守卫是否会被障碍卡住

当前 `MirrorGhostPair.prefab` 建议：
- `useBlockingDetection = 关`

原因：
- 这项目前更容易引入边缘 bug
- 关掉以后更适合先稳定搭图和测追击逻辑

## 7. 搭图检查清单

同学开始搭图前，先逐项确认：

- 左右相机 viewport 是否正确
- Player1 是否补了 `Level3PlayerAvatar + Level3PlayerLife`
- Player2 是否直接用 `CTS_Player_P2.prefab`
- 左右黑幕 Overlay 是否存在，且初始透明度为 0
- 梯子是否同时挂了 `LadderZone + PlayerTwoLadderZone`
- 营火是否挂了 `Level3Checkpoint`，且 `Side` 正确
- 死亡线是否挂了 `Level3DeathBoundary`
- 镜像类机关的 `mirrorCenterX` 是否和当前机关中心线一致
- 楼梯机关是否直接使用 `Level3_StairPuzzle_4Step.prefab`，而不是重新做父子嵌套

## 8. 建议的测试顺序

建议每搭一段就测一次，不要等整张图搭完再一起查 bug。

推荐顺序：

1. 先测两个玩家能不能分别移动、跳跃、爬梯
2. 再测营火和死亡复活
3. 再测普通箱子推动
4. 再测镜像箱子
5. 再测镜像守卫
6. 最后测四层楼梯机关

如果某个机关不对，优先先看：
- prefab 根节点上的脚本有没有引用丢失
- `mirrorCenterX` 和对象实际中心线是否一致
- Collider 是否初始重叠到平台里

## 9. 当前约定

目前 Level3 搭图时，默认按下面约定走：

- Player1 在左半屏，Player2 在右半屏
- 镜像机关都尽量用 prefab 自带的摆位同步，不手算两边坐标
- 四层楼梯机关统一用 `Level3_StairPuzzle_4Step.prefab`
- 旧 `PressurePlate.prefab` 不作为新的主机关方案
- 守卫优先使用 sensor 决定移动范围

如果后面你们要改规则，优先先改 README，再一起改 prefab 和脚本，避免搭图同学和写脚本的人理解不一致。
