// SPDX-License-Identifier: AGPL-3.0-only
package dev.arabicgames.phone;
final class LocalEndpoint {
 static String validate(String address){
  try{java.net.URI uri=new java.net.URI(address.trim());String host=uri.getHost();
   if(!"http".equals(uri.getScheme())||host==null||!dev.arabicgames.Pairing.privateIpv4(host)||uri.getUserInfo()!=null||uri.getQuery()!=null||uri.getFragment()!=null||uri.getPort()<1024||uri.getPort()>65535||!(uri.getPath().isEmpty()||uri.getPath().equals("/")))throw new IllegalArgumentException();
   return "http://"+host+":"+uri.getPort();
  }catch(Exception e){throw new IllegalArgumentException("أدخل عنوان كمبيوترك الخاص فقط، مثل http://192.168.0.10:8790 بدون مسار إضافي.");}
 }
}
