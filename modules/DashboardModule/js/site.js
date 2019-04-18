$(function () {
    var socket = io();

    var chartAllDevices = Highcharts.chart('chartAllDevices', {
      chart: {
        type: 'spline',
        animation: Highcharts.svg,
        events: { }
      },
      title: { text: 'Temperature by Device' },
      subtitle: { text: 'internal temperature' },
      xAxis: { type: 'datetime', tickPixelInterval: 200 },
      yAxis: { 
        title: { text: 'Temperature (F)' },
        plotLines: [
          { value: 0.5, width: 1, color: '#808080' },
          { value: 0, width: 1, color: '#808080' }
        ]
      },
      legend: {
        layout: 'vertical',
        align: 'right',
        verticalAlign: 'middle'
      },
      tooltip: {
        formatter: function () {
          return '<b>' + this.series.name + '</b>' +
          Highcharts.dateFormat(' - %H:%M:%S %d/%m/%Y- ', this.x) + '' +
          '<b>' + Highcharts.numberFormat(this.y, 2) + '</b>';
        }
      },
      plotOptions: {
        series: {
            label: {
                connectorAllowed: false
            }
        }
      },
      series: [
        {
          name: 'Dev367',
          data: (function () {
            // generate an array with 100 sample data (will size the data buffer)
            var data = [], time = (new Date()).getTime(), i;
            for (i = -100; i <= 0; i += 1) {
              data.push({
                x: time + i * 30000,
                y: 30
              });
            }
            return data;
          }())
        },
        {
            name: 'Dev7854',
            data: (function () {
              // generate an array with 100 sample data (will size the data buffer)
              var data = [], time = (new Date()).getTime(), i;
              for (i = -100; i <= 0; i += 1) {
                data.push({
                  x: time + i * 30000,
                  y: 30
                });
              }
              return data;
            }())
          }
      ],
      responsive: {
        rules: [{
            condition: {
                maxWidth: 500
            },
            chartOptions: {
                legend: {
                    layout: 'horizontal',
                    align: 'center',
                    verticalAlign: 'bottom'
                }
            }
        }]
      }
    });

    var chartDevice1 = Highcharts.chart('chartDevice1', {
        chart: {
          type: 'spline',
          animation: Highcharts.svg,
          events: { }
        },
        title: { text: 'Live data for Dev367' },
        subtitle: { text: 'machine and environment data' },
        xAxis: { type: 'datetime', tickPixelInterval: 200 },
        yAxis: { 
          title: { text: 'Value' },
          plotLines: [
            { value: 0.5, width: 1, color: '#808080' },
            { value: 0, width: 1, color: '#808080' }
          ]
        },
        legend: {
          layout: 'vertical',
          align: 'right',
          verticalAlign: 'middle'
        },
        tooltip: {
          formatter: function () {
            return '<b>' + this.series.name + '</b>' +
            Highcharts.dateFormat(' - %H:%M:%S %d/%m/%Y- ', this.x) + '' +
            '<b>' + Highcharts.numberFormat(this.y, 2) + '</b>';
          }
        },
        plotOptions: {
          series: {
              label: {
                  connectorAllowed: false
              }
          }
        },
        series: [
          {
            name: 'Machine/Temp',
            data: (function () {
              var data = [], time = (new Date()).getTime(), i;
              for (i = -100; i <= 0; i += 1) {
                data.push({
                  x: time + i * 30000,
                  y: 30
                });
              }
              return data;
            }())
          },
          {
              name: 'Machine/Pressure',
              data: (function () {
                var data = [], time = (new Date()).getTime(), i;
                for (i = -100; i <= 0; i += 1) {
                  data.push({
                    x: time + i * 30000,
                    y: 25
                  });
                }
                return data;
              }())
            },
            {
                name: 'Ambient/Temp',
                data: (function () {
                  var data = [], time = (new Date()).getTime(), i;
                  for (i = -100; i <= 0; i += 1) {
                    data.push({
                      x: time + i * 30000,
                      y: 28
                    });
                  }
                  return data;
                }())
              },
              {
                  name: 'Ambient/Humidity',
                  data: (function () {
                    var data = [], time = (new Date()).getTime(), i;
                    for (i = -100; i <= 0; i += 1) {
                      data.push({
                        x: time + i * 30000,
                        y: 80
                      });
                    }
                    return data;
                  }())
                }
        ],
        responsive: {
          rules: [{
              condition: {
                  maxWidth: 500
              },
              chartOptions: {
                  legend: {
                      layout: 'horizontal',
                      align: 'center',
                      verticalAlign: 'bottom'
                  }
              }
          }]
        }
      });
  
    // Called for each new message received from the IoT Edge module (PipeMessage method)
    socket.on('iot message', function(msg){
        $('#messages').append($('<li>').text(msg));

        var json = $.parseJSON(msg);
        var x = (new Date(json.TimeCreated)).getTime(); // UTC time

        var seriesTemperature;
        if (json.Machine.Id == "Dev367") {
            seriesTemperature = chartAllDevices.series[0];

            chartDevice1.series[0].addPoint([x, json.Machine.Temperature], true, true);
            chartDevice1.series[1].addPoint([x, json.Machine.Pressure], true, true);
            chartDevice1.series[2].addPoint([x, json.Ambient.Temperature], true, true);
            chartDevice1.series[3].addPoint([x, json.Ambient.Humidity], true, true);
        } 
        else if (json.Machine.Id == "Dev7854"){
            seriesTemperature = chartAllDevices.series[1];
        }

        seriesTemperature.addPoint([x, json.Machine.Temperature], true, true);
    });
  });