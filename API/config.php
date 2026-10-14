<?php
declare(strict_types=1);
if (!defined('PYRODUCK_API')) { http_response_code(404); exit; }
$config = [
    'admin_username' => 'admin',
    'admin_password_hash' => '', // Run setup.php from the command line.
    'require_https' => true,
    'storage_dir' => __DIR__ . '/private',
    'public_base_url' => 'https://pyroduck.com/API',
];
if (is_file(__DIR__ . '/config.local.php')) {
    $config = array_replace($config, require __DIR__ . '/config.local.php');
}
return $config;
