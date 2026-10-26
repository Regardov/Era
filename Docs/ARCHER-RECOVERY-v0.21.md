# ERA v0.21 — техническая причина и исправление лучника

## Подтверждённая причина

Ошибка была в прежнем `ArcherAnimationImporter.cs` из коммита `44e4d3f`, а не в последующем обновлении Regardov. FBX предоставляет редакторские PreviewAnimationClip. `Object.Instantiate(sourceClip)` / `EditorUtility.CopySerialized` сохранили их native class **1108** в 16 отдельных `.anim`. Editor мог сэмплировать эти объекты, поэтому прежние проверки прошли. Windows build исключал редакторский тип: в реально созданном игровом лучнике все 16 ссылок стали null. Animator, Generic Avatar и названия состояний при этом оставались исправными.

Доказательства: исходные YAML начинались `--- !u!1108 &110800000` / `PreviewAnimationClip:`; на актуальном v0.20 до исправления Windows-тест показал 16 null-клипов, пустые observedClips, нулевые движения Idle/Walk/Run при enabled Animator и valid Avatar. Эхо и Люмен двигались. [Сохранённый исходный результат](archer-runtime-before-recovery.json). История v0.20 не удаляла модель или клипы; изменение драйвера другом только убирало дублирующий универсальный VFX.

## Исправления

- Импортёр создаёт `new AnimationClip` и переносит существующие запечённые transform/object-reference кривые через AnimationUtility; копирует loop settings и события. Результат — native **class 74 / AnimationClip**, пригодный для Player. Это перенос прежней анимации, не повторное создание движений.
- Сохранены GUID всех 16 `.anim` и Ember.prefab. Внутренний fileID runtime-клипа правильно изменён **110800000 → 7400000**; ссылки в controller и Ember.prefab пересохранены. Сигнатура импортёра v4 запускает восстановление существующих ассетов.
- Сохранены Generic Avatar, 54 кости, root motion off, два слоя Archer.controller, transform mask. Общие KayKit-контроллеры/Avatar других персонажей не заменялись. Новая модель, 23 skinned meshes, материалы, крепления и авторитетная точка вылета стрелы сохранены.
- Драйвер теперь запрашивает верхний BowRelease также при автосрабатывании оружия во время Cast/Dash; базовое действие и его события продолжаются. Короткий release не расходуется до первого кадра оценки Animator. Presentation-событие не создаёт дополнительный игровой снаряд: урон и projectile остаются в Simulation.
- Новый runtime-тест измеряет настоящее движение костей, наличие всех клипов в Player, weighted release после каждого события, игровые стрелы, Echo/Lumen и конечность skinned vertices. Editor-проверка отдельно отклоняет PreviewAnimationClip.

## Сохранение исходников и последних обновлений

Blender `ArtSource/Characters/Archer/Archer_Animated.blend` открыт для инвентаризации: 16 Actions, 54 кости, 30 FPS, NLA tracks 0. FBX уже содержал нужные takes. Ни модель, ни Actions, ни FBX заново не генерировались. [Хеши и GUID](archer-preservation-v021.json).

Основа — main после fetch и fast-forward на `da3c206` / v0.20. Сохранены эффекты 129 предметов, звук, густые деревья, вода/карта, музыка и новые столкновения. Незавершённый прежний локальный draft v0.20 сохранён отдельно в stash, поверх обновления друга не применялся. История не переписана.

[Реальные проверки и ограничения](VALIDATION-v0.21.md).
