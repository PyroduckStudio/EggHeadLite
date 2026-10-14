<?php
declare(strict_types=1);
if (PHP_SAPI !== 'cli') { http_response_code(404); exit; }
fwrite(STDOUT, "Admin kullanici adi: ");
$username = trim((string) fgets(STDIN));
fwrite(STDOUT, "Parola (terminalde gorunebilir; 12-72 bayt): ");
$password = rtrim((string) fgets(STDIN), "\r\n");
if (!preg_match('/^[a-zA-Z0-9_.-]{3,40}$/D', $username) || strlen($password) < 12 || strlen($password) > 72) { fwrite(STDERR, "Gecersiz kullanici adi veya parola.\n"); exit(1); }
$path = __DIR__ . '/config.local.php';
$values = ['admin_username'=>$username, 'admin_password_hash'=>password_hash($password, PASSWORD_DEFAULT)];
if (is_file($path)) { define('PYRODUCK_API', true); $values = array_replace(require $path, $values); }
$source = "<?php\nif (!defined('PYRODUCK_API')) { http_response_code(404); exit; }\nreturn " . var_export($values, true) . ";\n";
if (file_put_contents($path, $source, LOCK_EX) === false) { fwrite(STDERR, "Ayar dosyasi yazilamadi.\n"); exit(1); }
chmod($path, 0600);
fwrite(STDOUT, "config.local.php hazir. Parolanin kendisi kaydedilmedi.\n");
