# Tips 

### Solution Cleanup
If you need to clean up the solution by removing both `bin` and `obj` folders from every project in the solution use powershell by executing the following commands using the solution root

```
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
```
Then 
```
./cleanup
```

### Build Assets

If the project is new, install all dependencenies from command line by executing

```
npm install
```

To build assets for all projects

```
gulp build-assets
```

If you want to force the creation of all assets

```
gulp build-assets --force
```
"# CrestApps.OrchardCore" 
