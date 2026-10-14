<?php
declare(strict_types=1);
define('PYRODUCK_API', true);
require __DIR__ . '/bootstrap.php';
header('Cache-Control: no-store');
header("Content-Security-Policy: default-src 'none'; style-src 'unsafe-inline'; form-action 'self'; frame-ancestors 'none'; base-uri 'none'");
if (!in_array($_SERVER['REQUEST_METHOD'], ['GET','POST'], true)) { http_response_code(405); header('Allow: GET, POST'); exit; }
if ($config['admin_password_hash'] === '') { http_response_code(503); exit('Yonetici hesabi kurulmamis. setup.php dosyasini terminalde calistirin.'); }
ini_set('session.use_strict_mode', '1');
ini_set('session.use_only_cookies', '1');
session_name('pyroduck_admin');
session_set_cookie_params(['lifetime'=>0, 'path'=>rtrim(dirname($_SERVER['SCRIPT_NAME']), '/') . '/', 'secure'=>$https, 'httponly'=>true, 'samesite'=>'Strict']);
session_start();
if (isset($_SESSION['last_seen']) && time() - $_SESSION['last_seen'] > 1800) $_SESSION = [];
$_SESSION['csrf'] ??= bin2hex(random_bytes(32));
$message = '';
$submitted = null;
if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    if (!is_string($_POST['csrf'] ?? null) || !hash_equals($_SESSION['csrf'], $_POST['csrf'])) { http_response_code(403); exit('Gecersiz form. Sayfayi yenileyin.'); }
    $action = $_POST['action'] ?? '';
    if ($action === 'login') {
        rate_limit('login', 10, 900);
        $username = $_POST['username'] ?? '';
        $password = $_POST['password'] ?? '';
        if (is_string($username) && is_string($password) && strlen($password) <= 72 && hash_equals($config['admin_username'], $username) && password_verify($password, $config['admin_password_hash'])) {
            session_regenerate_id(true);
            $_SESSION['authenticated'] = true;
            $_SESSION['last_seen'] = time();
            $_SESSION['csrf'] = bin2hex(random_bytes(32));
        } else { http_response_code(401); $message = 'Kullanici adi veya parola hatali.'; }
    } elseif (!empty($_SESSION['authenticated'])) {
        if ($action === 'logout') {
            $_SESSION = []; session_destroy();
            header('Location: admin.php', true, 303); exit;
        }
        if ($action === 'save') {
            try {
                $submitted = $_POST['catalog'] ?? '';
                if (!is_string($submitted) || strlen($submitted) > 262144) throw new InvalidArgumentException('JSON en fazla 256 KiB olmali.');
                $parsed = json_decode($submitted, true, 32, JSON_THROW_ON_ERROR);
                write_data('catalog', validate_catalog($parsed));
                $submitted = null;
                $message = 'Kaydedildi. Unity istemcilerinin onbellegi yenilendiginde gorunur.';
            } catch (JsonException | InvalidArgumentException $error) { http_response_code(422); $message = $error->getMessage(); }
        }
    } else { http_response_code(403); exit('Once giris yapin.'); }
}
$authenticated = !empty($_SESSION['authenticated']);
if ($authenticated) $_SESSION['last_seen'] = time();
function h(string $value): string { return htmlspecialchars($value, ENT_QUOTES | ENT_SUBSTITUTE, 'UTF-8'); }
$json = $authenticated ? ($submitted ?? json_encode(catalog(), JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE | JSON_THROW_ON_ERROR)) : '';
?>
<!doctype html>
<html lang="tr"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>PyroDuck Kampanya Yonetimi</title>
<style>body{font:16px system-ui;background:#101827;color:#edf2fa;margin:0}main{max-width:1000px;margin:48px auto;padding:24px}h1{color:#ffcf70}label{display:block;margin:16px 0 8px}input,textarea{box-sizing:border-box;width:100%;padding:12px;border:1px solid #53627b;border-radius:8px;background:#182337;color:#fff}textarea{min-height:520px;font:14px monospace}button{padding:12px 22px;background:#ffcf70;border:0;border-radius:8px;margin-top:16px;cursor:pointer}p{line-height:1.6}.message{padding:16px;background:#283953;border-radius:8px}.logout{float:right}a{color:#ffcf70}</style></head><body><main>
<h1>PyroDuck Kampanya Yonetimi</h1>
<?php if ($message !== ''): ?><p class="message" role="status"><?= h($message) ?></p><?php endif; ?>
<?php if (!$authenticated): ?>
<form method="post"><input type="hidden" name="csrf" value="<?= h($_SESSION['csrf']) ?>"><input type="hidden" name="action" value="login">
<label for="username">Kullanici adi</label><input id="username" name="username" required maxlength="40" autocomplete="username">
<label for="password">Parola</label><input id="password" type="password" name="password" required autocomplete="current-password"><button>Giris yap</button></form>
<?php else: ?>
<form method="post" class="logout"><input type="hidden" name="csrf" value="<?= h($_SESSION['csrf']) ?>"><button name="action" value="logout">Cikis yap</button></form>
<p>Metinleri, gorsel adresini, indirim oranini ve UTC tarihlerini duzenleyin. Tum reklamlari durdurmak icin en ustteki enabled degerini false yapin.</p>
<p>Istege bagli hedefleme: products: ["eggheads-lite"], languages: ["en"], minVersion: "1.0", maxVersion: "2.0". Hedeflenen alanlar icin API sorgusunda ilgili parametre gerekir.</p>
<p><a href="campaigns.php" target="_blank" rel="noopener">Varsayilan API yanitini ac</a></p>
<form method="post"><input type="hidden" name="csrf" value="<?= h($_SESSION['csrf']) ?>"><input type="hidden" name="action" value="save"><label for="catalog">Kampanya JSON</label><textarea id="catalog" name="catalog" spellcheck="false" required><?= h($json) ?></textarea><button>Kontrol et ve kaydet</button></form>
<?php endif; ?></main></body></html>
