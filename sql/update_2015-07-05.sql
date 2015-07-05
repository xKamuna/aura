CREATE TABLE `aura`.`cooldowns` (
  `id` SMALLINT(5) UNSIGNED NOT NULL,
  `creatureId` BIGINT(20) NOT NULL,
  `type` VARCHAR(50) NULL,
  `cooldownEndTime` DATETIME NULL DEFAULT NULL,
  PRIMARY KEY (`id`, `creatureId`),
  INDEX `creatureId` (`creatureId` ASC));
