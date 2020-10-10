-- --------------------------------------------------------
-- Host:                         127.0.0.1
-- Server Version:               8.0.16 - MySQL Community Server - GPL
-- Server Betriebssystem:        Win64
-- HeidiSQL Version:             10.2.0.5599
-- --------------------------------------------------------

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET NAMES utf8 */;
/*!50503 SET NAMES utf8mb4 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;


-- Exportiere Datenbank Struktur für dbot
CREATE DATABASE IF NOT EXISTS `dbot` /*!40100 DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci */ /*!80016 DEFAULT ENCRYPTION='N' */;
USE `dbot`;

-- Exportiere Struktur von Tabelle dbot.dbot_statistic_log
CREATE TABLE IF NOT EXISTS `dbot_statistic_log` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `log_date` timestamp NOT NULL,
  `guilds_count` int(11) DEFAULT NULL,
  `ping` int(11) DEFAULT NULL,
  `active_shards` int(11) DEFAULT NULL,
  `dbot_cpu` double DEFAULT NULL,
  `dbot_ram` double DEFAULT NULL,
  `dbot_uptime` time DEFAULT NULL,
  `lavalink_cpu` double DEFAULT NULL,
  `lavalink_ram` double DEFAULT NULL,
  `lavalink_uptime` time DEFAULT NULL,
  `lavalink_players_total` int(11) DEFAULT NULL,
  `lavalink_players_active` int(11) DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=27 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- Daten Export vom Benutzer nicht ausgewählt

/*!40101 SET SQL_MODE=IFNULL(@OLD_SQL_MODE, '') */;
/*!40014 SET FOREIGN_KEY_CHECKS=IF(@OLD_FOREIGN_KEY_CHECKS IS NULL, 1, @OLD_FOREIGN_KEY_CHECKS) */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
