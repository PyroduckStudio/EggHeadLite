<?php
declare(strict_types=1);
if (!defined('PYRODUCK_API')) { http_response_code(404); exit; }
$config = require __DIR__ . '/config.php';
ini_set('display_errors', '0');
set_exception_handler(function (Throwable $error): void {
    error_log('PyroDuck API: ' . $error->getMessage());
    http_response_code(503);
    header('Content-Type: application/json; charset=utf-8');
    header('Cache-Control: no-store');
    echo '{"error":"Service temporarily unavailable"}';
});
header('X-Content-Type-Options: nosniff');
header('Referrer-Policy: no-referrer');
$https = (!empty($_SERVER['HTTPS']) && $_SERVER['HTTPS'] !== 'off') || (($_SERVER['SERVER_PORT'] ?? '') == 443);
// Forwarded headers are deliberately not trusted; configure HTTPS in your host.
if (PHP_SAPI !== 'cli' && $config['require_https'] && !$https) {
    http_response_code(400);
    header('Content-Type: application/json');
    echo '{"error":"HTTPS required"}';
    exit;
}

function storage_path(string $name): string {
    global $config;
    $dir = $config['storage_dir'];
    if (!is_dir($dir) && !mkdir($dir, 0700, true) && !is_dir($dir)) {
        throw new RuntimeException('Cannot create storage directory');
    }
    return $dir . '/' . $name . '.php';
}

function read_data(string $name, $default) {
    $path = storage_path($name);
    if (!is_file($path)) return $default;
    $source = file_get_contents($path);
    $prefix = "<?php http_response_code(404); exit; ?>\n";
    if ($source === false || !str_starts_with($source, $prefix)) throw new RuntimeException('Invalid storage file');
    $json = substr($source, strlen($prefix));
    return json_decode($json, true, 32, JSON_THROW_ON_ERROR);
}

function write_data(string $name, $value): void {
    $path = storage_path($name);
    $json = json_encode($value, JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT | JSON_THROW_ON_ERROR);
    $temp = dirname($path) . '/write-' . bin2hex(random_bytes(16)) . '.php';
    $handle = fopen($temp, 'x');
    if ($handle === false) throw new RuntimeException('Cannot create temporary file');
    fclose($handle);
    try {
        chmod($temp, 0600);
        $source = "<?php http_response_code(404); exit; ?>\n" . $json;
        if (file_put_contents($temp, $source, LOCK_EX) === false || !rename($temp, $path)) {
            throw new RuntimeException('Cannot write storage');
        }
    } finally { if (is_file($temp)) unlink($temp); }
}

function rate_limit(string $group, int $maximum, int $window): void {
    $lock = fopen(storage_path('rate-' . $group . '-lock'), 'c');
    if (!$lock || !flock($lock, LOCK_EX)) throw new RuntimeException('Cannot lock rate limiter');
    try {
        $now = time();
        $items = read_data('rate-' . $group, []);
        foreach ($items as $key => $entry) if ($entry['until'] <= $now) unset($items[$key]);
        // Fixed buckets bound storage size. A bucket may be shared by several IPs.
        $key = substr(hash('sha256', $_SERVER['REMOTE_ADDR'] ?? 'local'), 0, 3);
        $entry = $items[$key] ?? ['until' => $now + $window, 'count' => 0];
        $blocked = $entry['count'] >= $maximum;
        if (!$blocked) $entry['count']++;
        $items[$key] = $entry;
        write_data('rate-' . $group, $items);
    } finally { flock($lock, LOCK_UN); fclose($lock); }
    if ($blocked) {
        http_response_code(429);
        header('Retry-After: ' . max(1, $entry['until'] - $now));
        header('Cache-Control: no-store');
        header('Content-Type: application/json');
        echo '{"error":"Too many requests"}';
        exit;
    }
}

function https_url(string $url, bool $destination): bool {
    $p = parse_url($url);
    if (!$p || ($p['scheme'] ?? '') !== 'https' || isset($p['user']) || isset($p['pass']) || ($p['port'] ?? 443) !== 443) return false;
    $host = strtolower($p['host'] ?? '');
    if (!preg_match('/^[a-z0-9.-]+\.[a-z]{2,}$/D', $host) || str_ends_with($host, '.local')) return false;
    return !$destination || $host === 'assetstore.unity.com' || $host === 'pyroduck.com' || str_ends_with($host, '.pyroduck.com');
}

function valid_date(string $value): bool {
    if ($value === '') return true;
    if (!preg_match('/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,6})?Z$/D', $value)) return false;
    $date = DateTimeImmutable::createFromFormat('!Y-m-d\TH:i:s\Z', substr($value, 0, 19) . 'Z', new DateTimeZone('UTC'));
    return $date !== false && $date->format('Y-m-d\TH:i:s\Z') === substr($value, 0, 19) . 'Z';
}

function validate_catalog($data): array {
    if (!is_array($data) || ($data['schemaVersion'] ?? null) !== 1 || !is_bool($data['enabled'] ?? null) ||
        !isset($data['campaigns']) || !is_array($data['campaigns']) || !array_is_list($data['campaigns']) || count($data['campaigns']) > 20) {
        throw new InvalidArgumentException('schemaVersion=1, enabled boolean ve en fazla 20 kampanya gerekli.');
    }
    $ids = [];
    foreach ($data['campaigns'] as $card) {
        if (!is_array($card)) throw new InvalidArgumentException('Kampanya bir nesne olmali.');
        foreach (['id'=>100, 'title'=>100, 'description'=>500, 'badge'=>80, 'imageUrl'=>2048, 'targetUrl'=>2048, 'startUtc'=>40, 'endUtc'=>40] as $field=>$limit) {
            if (!isset($card[$field]) || !is_string($card[$field]) || strlen($card[$field]) > $limit) throw new InvalidArgumentException('Gecersiz alan: ' . $field);
        }
        if (trim($card['id']) === '' || trim($card['title']) === '' || isset($ids[$card['id']])) throw new InvalidArgumentException('ID ve baslik gerekli; ID benzersiz olmali.');
        $ids[$card['id']] = true;
        if (isset($card['productId']) && (!is_string($card['productId']) || !preg_match('/^[a-zA-Z0-9._-]{1,100}$/D', $card['productId']))) throw new InvalidArgumentException('Gecersiz productId.');
        foreach (['enabled','active'] as $field) if (!is_bool($card[$field] ?? null)) throw new InvalidArgumentException($field . ' boolean olmali.');
        if (!is_int($card['priority'] ?? null) || abs($card['priority']) > 100000 || !is_int($card['discountPercent'] ?? null) || $card['discountPercent'] < 0 || $card['discountPercent'] > 100) throw new InvalidArgumentException('Gecersiz oncelik veya indirim.');
        if (!https_url($card['targetUrl'], true) || ($card['imageUrl'] !== '' && !https_url($card['imageUrl'], false))) throw new InvalidArgumentException('Gecersiz HTTPS baglantisi.');
        if (!valid_date($card['startUtc']) || !valid_date($card['endUtc']) || ($card['startUtc'] !== '' && $card['endUtc'] !== '' && new DateTimeImmutable($card['startUtc']) >= new DateTimeImmutable($card['endUtc']))) throw new InvalidArgumentException('Gecersiz UTC tarih araligi.');
        foreach (['products','languages'] as $field) {
            if (!isset($card[$field])) continue;
            if (!is_array($card[$field]) || !array_is_list($card[$field]) || count($card[$field]) > 20) throw new InvalidArgumentException('Gecersiz hedefleme: ' . $field);
            foreach ($card[$field] as $value) if (!is_string($value) || !preg_match('/^[a-z0-9-]{1,40}$/D', $value)) throw new InvalidArgumentException('Gecersiz hedefleme degeri.');
        }
        foreach (['minVersion','maxVersion'] as $field) if (isset($card[$field]) && (!is_string($card[$field]) || !preg_match('/^\d{1,5}(?:\.\d{1,5}){0,3}$/D', $card[$field]))) throw new InvalidArgumentException('Gecersiz surum.');
        if (isset($card['minVersion'], $card['maxVersion']) && version_compare($card['minVersion'], $card['maxVersion'], '>')) throw new InvalidArgumentException('Surum araligi ters.');
    }
    return $data;
}

function catalog(): array {
    global $config;
    $data = read_data('catalog', null);
    if ($data === null) {
        $data = json_decode(file_get_contents(__DIR__ . '/campaigns.example.json'), true, 32, JSON_THROW_ON_ERROR);
        $data['campaigns'][0]['imageUrl'] = rtrim($config['public_base_url'], '/') . '/images/egg-heads-full-v1.png';
    }
    return validate_catalog($data);
}
