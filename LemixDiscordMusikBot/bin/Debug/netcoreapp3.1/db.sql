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

-- Exportiere Struktur von Tabelle dbot.guilds
CREATE TABLE IF NOT EXISTS `guilds` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `Json` json NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=34 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- Exportiere Daten aus Tabelle dbot.guilds: ~1 rows (ungefähr)
/*!40000 ALTER TABLE `guilds` DISABLE KEYS */;
INSERT INTO `guilds` (`id`, `Json`) VALUES
	(1, '{"GuildRoles": {"696753707479597086": [{"Item1": {"id": 696779042426191972, "name": "Admin", "color": 15158332, "hoist": true, "Mention": "<@&696779042426191972>", "managed": false, "position": 6, "mentionable": false, "permissions": 372760297}, "Item2": 1}, {"Item1": {"id": 696779042426191972, "name": "Admin", "color": 15158332, "hoist": true, "Mention": "<@&696779042426191972>", "managed": false, "position": 6, "mentionable": false, "permissions": 372760297}, "Item2": 0}]}, "BotChannels": {"667842782190239785": 734387760856563783, "696753707479597086": 751551971852288020}, "AnnounceStates": {"667842782190239785": true, "696753707479597086": true}, "CheckAFKStates": {"667842782190239785": true, "696753707479597086": true}, "TrackLoadPlaylists": null, "FavoritesTracksLists": {"667842782190239785": [{"uri": "https://www.youtube.com/watch?v=6GCNUeTFSbA", "title": "Maniac", "author": "Michael Sembello - Topic", "length": 245000, "isStream": false, "position": 0, "identifier": "6GCNUeTFSbA", "isSeekable": true}, {"uri": "https://www.youtube.com/watch?v=_RYBDTnS7dg", "title": "Rise Against - Re-Education (Through Labor) (Uncensored) [Official Video]", "author": "RiseAgainst", "length": 240000, "isStream": false, "position": 0, "identifier": "_RYBDTnS7dg", "isSeekable": true}], "696753707479597086": [{"uri": "https://www.youtube.com/watch?v=cbqfQ12V6_M", "title": "Flashdance - Maniac ♥", "author": "Melissa Ramírez R", "length": 241000, "isStream": false, "position": 0, "identifier": "cbqfQ12V6_M", "isSeekable": true}, {"uri": "https://www.youtube.com/watch?v=taOL5HJdx1A", "title": "Lighthouse Family - High (Official Video)", "author": "Lighthouse Family", "length": 313000, "isStream": false, "position": 0, "identifier": "taOL5HJdx1A", "isSeekable": true}, {"uri": "https://www.youtube.com/watch?v=2JNdwqfmS20", "title": "Fuck, Marry, Kill: The Game Show (NSFW) - {The Kloons}", "author": "The Kloons", "length": 457000, "isStream": false, "position": 0, "identifier": "2JNdwqfmS20", "isSeekable": true}, {"uri": "https://www.youtube.com/watch?v=nxyjzYKSZz0", "title": "Geile Musik Zum Zocken 2020 🎮 Bass Boosted Best Trap Mix 🎮 Musik Deutsch 2020", "author": "DK Media", "length": 2305000, "isStream": false, "position": 0, "identifier": "nxyjzYKSZz0", "isSeekable": true}, {"uri": "https://www.youtube.com/watch?v=Vnoz5uBEWOA", "title": "Kiesza - Hideaway (Official Music Video)", "author": "Kiesza", "length": 275000, "isStream": false, "position": 0, "identifier": "Vnoz5uBEWOA", "isSeekable": true}, {"uri": "https://www.youtube.com/watch?v=pjJ2w1FX_Wg", "title": "Carla - Bim Bam toi (Clip Officiel)", "author": "CarlamusicoffVEVO", "length": 173000, "isStream": false, "position": 0, "identifier": "pjJ2w1FX_Wg", "isSeekable": true}]}, "BotChannelMainMessages": {"667842782190239785": 734387770361118741, "696753707479597086": 751551976785051821}, "BotChannelBannerMessages": {"667842782190239785": 734387765227159582, "696753707479597086": 751551976289992825}}');
/*!40000 ALTER TABLE `guilds` ENABLE KEYS */;

/*!40101 SET SQL_MODE=IFNULL(@OLD_SQL_MODE, '') */;
/*!40014 SET FOREIGN_KEY_CHECKS=IF(@OLD_FOREIGN_KEY_CHECKS IS NULL, 1, @OLD_FOREIGN_KEY_CHECKS) */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
