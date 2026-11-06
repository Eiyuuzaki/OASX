package com.oas.oasx.mobile.net

import java.util.concurrent.TimeUnit
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody

/// 后端 HTTP 客户端：baseUrl 来自设置页（SharedPreferences 持久化）。
/// 后端监听 0.0.0.0:22288，手机与 PC 需在同一局域网。
object Api {
    @Volatile
    var baseUrl: String = ""

    val json = Json { ignoreUnknownKeys = true }

    val client = OkHttpClient.Builder()
        .connectTimeout(6, TimeUnit.SECONDS)
        .readTimeout(30, TimeUnit.SECONDS)
        .build()

    private val jsonType = "application/json; charset=utf-8".toMediaType()

    private fun url(path: String): String = baseUrl.trimEnd('/') + path

    private fun fail(method: String, path: String, code: Int, body: String): Nothing =
        throw RuntimeException("$method $path -> HTTP $code: ${body.take(160)}")

    /// GET 原始文本（失败抛异常，由页面捕获显示）。
    fun get(path: String): String {
        val req = Request.Builder().url(url(path)).build()
        client.newCall(req).execute().use { resp ->
            val body = resp.body?.string() ?: ""
            if (!resp.isSuccessful) fail("GET", path, resp.code, body)
            return body
        }
    }

    /// POST 空体（foster/refresh、notify/test 等端点）。
    fun post(path: String): String = postWithBody(path, "")

    /// POST 带 JSON 体。
    fun postWithBody(path: String, jsonBody: String): String {
        val req = Request.Builder()
            .url(url(path))
            .post(jsonBody.toRequestBody(jsonType))
            .build()
        client.newCall(req).execute().use { resp ->
            val body = resp.body?.string() ?: ""
            if (!resp.isSuccessful) fail("POST", path, resp.code, body)
            return body
        }
    }

    /// PUT 带 JSON 体（配置写入端点）。
    fun put(path: String, jsonBody: String): String {
        val req = Request.Builder()
            .url(url(path))
            .put(jsonBody.toRequestBody(jsonType))
            .build()
        client.newCall(req).execute().use { resp ->
            val body = resp.body?.string() ?: ""
            if (!resp.isSuccessful) fail("PUT", path, resp.code, body)
            return body
        }
    }

    /// 网络错误（超时/HTTP 非 2xx）原样抛出；仅解析失败标记为"响应解析失败"。
    inline fun <reified T> getJsonOrThrow(path: String): T = try {
        json.decodeFromString(get(path))
    } catch (e: kotlinx.serialization.SerializationException) {
        throw RuntimeException("响应解析失败：$path")
    }

    /// POST 空体 + 解析。
    inline fun <reified T> postJsonOrThrow(path: String): T = try {
        json.decodeFromString(post(path))
    } catch (e: kotlinx.serialization.SerializationException) {
        throw RuntimeException("响应解析失败：$path")
    }

    /// PUT + 解析。
    inline fun <reified T> putJsonOrThrow(path: String, jsonBody: String): T = try {
        json.decodeFromString(put(path, jsonBody))
    } catch (e: kotlinx.serialization.SerializationException) {
        throw RuntimeException("响应解析失败：$path")
    }

    /// GET /config_list → 配置名数组。
    fun configList(): List<String> = json.decodeFromString(get("/config_list"))
}