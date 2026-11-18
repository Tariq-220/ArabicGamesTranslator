package dev.arabicgames.phone;
final class GameNames {
 static String key(String game){if(game==null)return "";String name=game.trim().toLowerCase(java.util.Locale.ROOT).replaceAll("\\s+"," ");if(name.equals("rdr2")||name.equals("red dead redemption 2"))return "rdr2";if(name.equals("gta6")||name.equals("gta 6")||name.equals("grand theft auto vi")||name.equals("grand theft auto 6"))return "gta6";return name;}
 static boolean same(String a,String b){return !key(a).isEmpty()&&key(a).equals(key(b));}
}
