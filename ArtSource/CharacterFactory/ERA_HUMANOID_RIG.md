# ERA_Humanoid54_v1 — совместимый скелет

Это контракт существующего лучника, а не универсальный автогенератор рига. Полные rest matrices, head/tail, родители и hashes Actions хранятся в [rig-schema.json](rig-schema.json). Источник: `ArtSource/Characters/Archer/Archer_Animated.blend`. Файл шаблона — `ERA_CharacterLibrary.blend`.

## Пространство и версия

- Blender 5.2.3 LTS; Z up, −Y forward, метры, H=1,8 м с капюшоном. Unity-проверка выполнена в 2023.1.22f1.
- Armature object должен экспортироваться как **Archer_Rig**. Data-block называется ERA_Humanoid54_v1; data-block name не заменяет transform path.
- Generic paths исходных .anim начинаются `Archer_Rig/Root/...`. Суффикс `.001`, новый родитель внутри skeleton или перестановка костей ломают контракт путей.
- Core: 24 кости. Ещё 30 — существующие extension bones оружия, одежды и накладок. В совместимой v1 остаются все 54, даже если к некоторым не привязаны меши нового персонажа.
- Rest pose — существующая опущенная постановка рук. Не переводить её в T-pose с Apply Pose as Rest ради внешнего вида. Отдельный control rig допустим только если запекает движение обратно в неизменный экспортный skeleton.

## Измеренная иерархия

```text
Root
  Pelvis
    Spine
      Chest
        Neck
          Head
        Clavicle.L
          UpperArm.L
            Forearm.L
              Hand.L
                Fingers.L
                BowRoot
                  BowUpper1
                    BowUpper2
                      StringTipUpper
                  BowLower1
                    BowLower2
                      StringTipLower
                  ArrowNock
                    ArrowHeld
                    ArrowSpawn
                WeaponSocket
          ShoulderPad.L
        Clavicle.R
          UpperArm.R
            Forearm.R
              Hand.R
                Fingers.R
          ShoulderPad.R
        Cape1.L
          Cape2.L
        Cape1.C
          Cape2.C
        Cape1.R
          Cape2.R
        QuiverAttach
    Thigh.L
      Shin.L
        Foot.L
          Toe.L
      KneePad.L
    PanelFront1.L
      PanelFront2.L
    PanelSide.L
    Thigh.R
      Shin.R
        Foot.R
          Toe.R
      KneePad.R
    PanelFront1.R
      PanelFront2.R
    PanelSide.R
    Tabard1
      Tabard2
```

Core перечислен в JSON. ShoulderPad, KneePad, Cape, Panel и Tabard — анимационные расширения, а не обязательные предметы одежды каждого героя. Bow/String/Arrow/Quiver — расширения лучника. Их пустое присутствие сохраняет пути; их меши по умолчанию не добавлены в базу. Удаление этих цепей создаёт новый, отдельно проверяемый вариант схемы.

## Веса и модификаторы

1. Armature modifier каждого skinned-компонента указывает на один рабочий rig. Проверить Root Bone в импортированном SkinnedMeshRenderer. Никаких одновременно работающих копий старого/new rig.
2. Все deform-веса вершины суммируются в 1 с допуском 1e−4. PART_* — селекторы происхождения; они не входят в эту сумму. Максимум четыре влияния для целевой Unity-подготовки; у проверенной тестовой базы максимум два.
3. Маленькие жёсткие пластины и подсумки: один вес соответствующей кости; длинная одежда: осмысленные переходы на существующие цепи. Не привязывать целый плащ к одной ноге.
4. Переходы через плечо, локоть, колено и запястье проверять на крайних позах. Нормализованные веса сами по себе не гарантируют хорошую деформацию.
5. Острова одежды сохраняются; не склеивать все пересечения remesh/merge-by-distance.
6. Fingers.L/R — по одной общей кости на кисть. Независимых пяти пальцев и лицевой мимической системы в этом риге нет.

## Крепления

- Hand.L → BowRoot / WeaponSocket — текущая переноска лука.
- BowRoot → цепи плеч лука, StringTipUpper/Lower, ArrowNock → ArrowHeld → ArrowSpawn — анимируемые элементы натяжения и выпуска.
- Hand.R — правая кисть; другое оружие может получить отдельный дочерний Empty/socket, сохраняя существующие пути костей. Не назначать ему левый BowRoot автоматически.
- QuiverAttach — жёсткая посадка колчана со стрелами на Chest.
- ShoulderPad.L/R, KneePad.L/R — отдельные накладки; Cape1/2 — нижняя ткань; PanelFront/Side и Tabard — одежда.

Muzzle измеряется в фактической позе выпуска и согласуется с Simulation/событием. Направление ArrowSpawn в rest pose не является готовым направлением выстрела. Наличие socket не подменяет игровые таймеры и логику снаряда.

## Банк неизменённых Actions

В библиотеке 26 существующих Actions, FPS=30, NLA tracks=0, pose constraints=0. Движения уже запечены в FK location/quaternion/scale. Прямое назначение использует Blender 5 action slot; helpers не обращаются к старому action.fcurves API.

| Action | Секунды | Цикл по Unity manifest | События manifest |
|---|---:|:---:|---|
| Archer_Idle | 2.400 | да | — |
| Archer_Walk | 0.933 | да | — |
| Archer_Run | 0.600 | да | — |
| Archer_Aim | 0.500 | нет | — |
| Archer_BowDraw | 0.600 | нет | — |
| Archer_BowHold | 1.200 | да | — |
| Archer_BowRelease | 0.300 | нет | SpawnArrow |
| Archer_ShootRecovery | 0.400 | нет | — |
| Archer_DodgeRoll | 0.700 | нет | — |
| Archer_Dash | 0.400 | нет | — |
| Archer_Hit | 0.333 | нет | — |
| Archer_Death | 1.500 | нет | — |
| Archer_Cast | 0.800 | нет | CastActivate |
| Archer_Interact | 0.600 | нет | — |
| Archer_TeleportOut | 0.800 | нет | TeleportDisappear |
| Archer_TeleportIn | 0.600 | нет | TeleportComplete |
| Archer_WalkLeft | 0.933 | да | — |
| Archer_WalkRight | 0.933 | да | — |
| Archer_WalkBack | 0.933 | да | — |
| Archer_RunLeft | 0.600 | да | — |
| Archer_RunRight | 0.600 | да | — |
| Archer_RunBack | 0.600 | да | — |
| Archer_JumpStart | 0.200 | нет | — |
| Archer_Airborne | 1.000 | да | — |
| Archer_Fall | 0.800 | да | — |
| Archer_Land | 0.267 | нет | — |

Loop settings и события — данные Unity manifest/.anim, а не гарантированно одинаковые свойства Blender Actions/FBX takes. Существующие .anim следует сохранять вместе с .meta. Не переписывать их при сборке нового персонажа. Для правки движения сделать отдельную копию Action с новым именем и назначением.

## Что означает совместимость

- **Точно та же схема + rest matrices + пути:** существующие Generic .anim могут управлять новым мешем. В изолированном Unity-проекте проверены все 26 оригинальных .anim на тестовой копии, без missing transform paths.
- **Только mesh-пропорции при неизменном skeleton:** curves связываются, но новые объёмы/веса могут пересекаться. Проверять все нужные позы и предметы.
- **Другие длины/ориентации костей или rest pose:** прежняя локальная трансляция/ротация не является автоматическим ретаргетингом. scale_region(change_rest=True) помечает необходимость rebake и блокирует bind_action / animated export. Автоматического универсального ретаргетера в наборе нет.
- **Humanoid Avatar:** не создавался и не проверялся. Переход на Humanoid — отдельная задача mapping/rest pose/дополнительных костей, не просто смена пункта Import Settings.
- **Legacy/KayKit 41/42 кости:** другая архитектура ERA; этот стандарт её не заменяет.

## Unity-экспорт

Экспортировать одну рабочую Armature и выбранные меши через export_unity(). Source scale=1, axis_forward='-Z', axis_up='Y', add_leaf_bones=False, use_armature_deform_only=False, bake_anim_use_all_bones=True, NLA=False, all_actions=True, step=1, simplify=0. В новом экспортном файле должны быть только нужные совместимые Actions; посторонние banks запрещены проверкой.

Importer: Generic, Avatar CreateFromThisModel, motion node Root, importAnimation/resampleCurves on, animationCompression Off для контрольного импорта, optimizeGameObjects off, importNormals Import, no cameras/lights. Root Motion off. Масштаб игрового роста устанавливается отдельно.

Для существующего ERA controller: Base locomotion → BodyActions → UpperBody. Маски и bow events специфичны текущей архитектуре; без требуемой экипировки/приёмников событий нельзя объявлять новую модель готовым игровым лучником. В изолированном тесте скопированы оригинальные controller/masks/26 .anim; это Editor import/sample validation, не интеграция в действующую игру.

## Проверка и пределы

- [Blender smoke](Measurements/factory-validation.json): независимые данные мешей/материалов, 26 неизменённых Actions, веса/геометрия, экспорт FBX, запрет редактирования reference и прямого Action reuse после rest edit.
- [Unity probe](Measurements/unity-validation.json): 54 кости, 17 skinned-мешей тестовой сборки, Generic Avatar, 26 FBX takes, 26 оригинальных .anim, три слоя контроллера, нулевые missing paths, конечная геометрия при выборке пяти времён каждого клипа.
- Прямое совпадение paths и конечные вершины не доказывают идеальные контакты/постановку стоп. Новый герой требует runtime-проверки своего prefab, событий и используемого оружия.
